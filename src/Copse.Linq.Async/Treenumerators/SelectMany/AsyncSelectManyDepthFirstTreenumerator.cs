using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  // The pointed bind, depth-first and streaming: every source node is replaced in place by
  // its expansion's forest, and the node's own children -- each replaced the same way --
  // re-hang at the expansion's slot. Source nodes never appear in the output; their visits
  // are structure only, carried by a stack of FRAMES (one per open source node) that says
  // where that node's replacement went and where its children go.
  //
  // The driver is the lab's stack of paused enumerators one carrier up: a frame drains its
  // forest's visit stream (roots re-indexed into the frame's SLOT, deeper nodes offset by the
  // slot's depth), then splices the source subtree, pulling the source one event at a time.
  // Contract: nothing is pulled ahead of its emission. The one thing held is a forest ROOT's
  // visit, released when the next forest event arrives -- a phase lag of one event in the
  // same order, never an inversion -- because a visit stream cannot say which visit of a root
  // is its last without that next event, and the last root's last visit is where UnderLastRoot
  // opens its slot.
  //
  // Visits of the node that OWNS a slot are manufactured here, one after each root emitted
  // into the slot, continuing that node's own visit count: the output obeys the visit
  // protocol (a parent is visited between and after its children) whatever forest the
  // children came from. A pending queue carries manufactured and released emissions, one
  // per pull.
  internal sealed class AsyncSelectManyDepthFirstTreenumerator<TSource, TResult> : AsyncTreenumeratorBase<TResult>
  {
    public AsyncSelectManyDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TSource>> sourceTreenumeratorFactory,
      Func<TSource, AsyncExpansion<TResult>> selector)
    {
      _Source = sourceTreenumeratorFactory();
      _Selector = selector;
      _RootSlot = new Slot(depth: 0);
    }

    private readonly IAsyncTreenumerator<TSource> _Source;
    private readonly Func<TSource, AsyncExpansion<TResult>> _Selector;
    private readonly Slot _RootSlot;
    private readonly List<Frame> _Frames = new List<Frame>();
    private readonly Queue<Emission> _Pending = new Queue<Emission>();
    private bool _SourceExhausted;
    private Frame _LastScheduledFrame;

    // A slot: an output depth, a running sibling index for the roots emitted there, and --
    // when the slot is under a node -- that node, for the visits manufactured after each root.
    private sealed class Slot
    {
      public Slot(int depth)
      {
        Depth = depth;
      }

      public Slot(int depth, TResult parent, int parentSiblingIndex, int parentVisitCount, int nextSiblingIndex)
      {
        Depth = depth;
        HasParent = true;
        Parent = parent;
        ParentSiblingIndex = parentSiblingIndex;
        ParentVisitCount = parentVisitCount;
        NextSiblingIndex = nextSiblingIndex;
      }

      public readonly int Depth;
      public readonly bool HasParent;
      public readonly TResult Parent;
      public readonly int ParentSiblingIndex;
      public int ParentVisitCount;
      public int NextSiblingIndex;
    }

    private enum Phase
    {
      DrainingForest,
      Splicing,
      SkippingChildren,
    }

    private sealed class Frame
    {
      public int SourceDepth;
      public SlotPlacement Placement;
      public bool SingleValuePending;                   // a one-node forest, scheduled, its visit not yet released
      public TResult SingleValue;
      public IAsyncTreenumerator<TResult> Forest;        // null: no forest, or drained
      public Slot ForestSlot;                            // where the forest's roots go
      public Slot ChildSlot;                             // where the children's replacements go
      public Phase Phase;
      public NodeTraversalStrategies ForestStrategies = NodeTraversalStrategies.TraverseAll;
      public bool HasHeld;
      public Emission Held;                              // a forest root's visit, awaiting the next event
      public int CurrentRootSiblingIndex;                // the output index of the forest root now open
      public int SkipSpliceCandidateRoot = -1;           // a root the consumer skipped the descendants of
      public bool LastRootReceivesSplice;                // UnderLastRoot realized on a nonempty forest
      public bool SkipDescendantsOwed;                   // the next source pull skips the node's subtree
    }

    private struct Emission
    {
      public TResult Node;
      public NodePosition Position;
      public int VisitCount;
      public TreenumeratorMode Mode;
      public Frame ScheduledBy;
    }

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Consumer strategies bind to the last SCHEDULED node, always a forest node; visits take none.
      if (Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies != NodeTraversalStrategies.TraverseAll
        && _LastScheduledFrame != null)
        NoteStrategies(_LastScheduledFrame, nodeTraversalStrategies);

      while (true)
      {
        if (_Pending.Count > 0)
        {
          Publish(_Pending.Dequeue());
          return true;
        }

        if (_Frames.Count == 0)
        {
          if (_SourceExhausted)
            return false;

          await PullSourceAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false);
          continue;
        }

        var top = _Frames[_Frames.Count - 1];

        if (top.Phase == Phase.DrainingForest)
        {
          await DrainForestAsync(top).ConfigureAwait(false);
          continue;
        }

        var strategies = top.SkipDescendantsOwed ? NodeTraversalStrategies.SkipDescendants : NodeTraversalStrategies.TraverseAll;
        top.SkipDescendantsOwed = false;

        await PullSourceAsync(strategies).ConfigureAwait(false);
      }
    }

    private void NoteStrategies(Frame frame, NodeTraversalStrategies strategies)
    {
      frame.ForestStrategies = strategies;

      if (Position.Depth == frame.ForestSlot.Depth
        && strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        frame.SkipSpliceCandidateRoot = frame.CurrentRootSiblingIndex;
    }

    private async ValueTask DrainForestAsync(Frame frame)
    {
      if (frame.SingleValuePending)
      {
        ReleaseSingleValue(frame);
        return;
      }

      if (frame.Forest == null)
      {
        OnForestDrained(frame);
        return;
      }

      var strategies = frame.ForestStrategies;
      frame.ForestStrategies = NodeTraversalStrategies.TraverseAll;

      if (!await frame.Forest.MoveNextAsync(strategies).ConfigureAwait(false))
      {
        await frame.Forest.DisposeAsync().ConfigureAwait(false);
        frame.Forest = null;
        OnForestDrained(frame);
        return;
      }

      var forest = frame.Forest;
      var isRoot = forest.Position.Depth == 0;
      var startsRoot = isRoot && forest.Mode == TreenumeratorMode.SchedulingNode;

      if (frame.HasHeld)
      {
        frame.HasHeld = false;
        _Pending.Enqueue(frame.Held);

        if (startsRoot)
          EnqueueSlotParentVisit(frame.ForestSlot);    // the previous root completed
      }

      if (startsRoot)
        frame.CurrentRootSiblingIndex = frame.ForestSlot.NextSiblingIndex++;

      var emission = new Emission
      {
        Node = forest.Node,
        Mode = forest.Mode,
        VisitCount = forest.VisitCount,
        Position = new NodePosition(
          isRoot ? frame.CurrentRootSiblingIndex : forest.Position.SiblingIndex,
          frame.ForestSlot.Depth + forest.Position.Depth),
        ScheduledBy = frame,
      };

      if (isRoot && forest.Mode == TreenumeratorMode.VisitingNode)
      {
        frame.HasHeld = true;
        frame.Held = emission;
      }
      else
      {
        _Pending.Enqueue(emission);
      }
    }

    // The one-node forest, structurally: schedule now; on the next pull -- after the consumer
    // has had its say about the scheduled node -- hold the visit (unless the node was skipped)
    // and fall through to the drained state. Same timing as the treenumerator path, no
    // treenumerator.
    private void ScheduleSingleValue(Frame frame, TResult value)
    {
      frame.CurrentRootSiblingIndex = frame.ForestSlot.NextSiblingIndex++;
      frame.SingleValue = value;
      frame.SingleValuePending = true;

      _Pending.Enqueue(new Emission
      {
        Node = value,
        Mode = TreenumeratorMode.SchedulingNode,
        VisitCount = 0,
        Position = new NodePosition(frame.CurrentRootSiblingIndex, frame.ForestSlot.Depth),
        ScheduledBy = frame,
      });
    }

    private void ReleaseSingleValue(Frame frame)
    {
      frame.SingleValuePending = false;

      if (!frame.ForestStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        frame.HasHeld = true;
        frame.Held = new Emission
        {
          Node = frame.SingleValue,
          Mode = TreenumeratorMode.VisitingNode,
          VisitCount = 1,
          Position = new NodePosition(frame.CurrentRootSiblingIndex, frame.ForestSlot.Depth),
        };
      }

      frame.ForestStrategies = NodeTraversalStrategies.TraverseAll;
      OnForestDrained(frame);
    }

    private void OnForestDrained(Frame frame)
    {
      if (frame.HasHeld)
      {
        frame.HasHeld = false;
        _Pending.Enqueue(frame.Held);                    // the last root's last visit

        if (frame.Placement == SlotPlacement.UnderLastRoot)
        {
          frame.LastRootReceivesSplice = true;
          frame.ChildSlot = new Slot(
            depth: frame.Held.Position.Depth + 1,
            parent: frame.Held.Node,
            parentSiblingIndex: frame.Held.Position.SiblingIndex,
            parentVisitCount: frame.Held.VisitCount,
            nextSiblingIndex: frame.Held.VisitCount - 1);
        }
        else
        {
          EnqueueSlotParentVisit(frame.ForestSlot);      // the last root completed
        }
      }

      if (!frame.LastRootReceivesSplice)
        frame.ChildSlot = frame.ForestSlot;              // AfterRoots, None, or UnderLastRoot on nothing

      var skipSplice = frame.Placement == SlotPlacement.None
        || (frame.SkipSpliceCandidateRoot >= 0 && frame.SkipSpliceCandidateRoot == frame.CurrentRootSiblingIndex);

      frame.Phase = skipSplice ? Phase.SkippingChildren : Phase.Splicing;
      frame.SkipDescendantsOwed = skipSplice;
    }

    private async ValueTask PullSourceAsync(NodeTraversalStrategies strategies)
    {
      if (!await _Source.MoveNextAsync(strategies).ConfigureAwait(false))
      {
        _SourceExhausted = true;
        PopFramesDeeperThan(-1);
        return;
      }

      var depth = _Source.Position.Depth;

      if (_Source.Mode == TreenumeratorMode.SchedulingNode)
      {
        PopFramesDeeperThan(depth - 1);

        var parent = _Frames.Count > 0 ? _Frames[_Frames.Count - 1] : null;
        var expansion = _Selector(_Source.Node);

        var frame = new Frame
        {
          SourceDepth = depth,
          Placement = expansion.Placement,
          Forest = expansion.HasSingleValue ? null : expansion.Forest?.GetAsyncDepthFirstTreenumerator(),
          ForestSlot = parent == null ? _RootSlot : parent.ChildSlot,
          Phase = Phase.DrainingForest,
        };

        _Frames.Add(frame);

        if (expansion.HasSingleValue)
          ScheduleSingleValue(frame, expansion.SingleValue);
      }
      else
      {
        PopFramesDeeperThan(depth);                      // the visit of an open node: structure only
      }
    }

    private void PopFramesDeeperThan(int depth)
    {
      while (_Frames.Count > 0 && _Frames[_Frames.Count - 1].SourceDepth > depth)
      {
        var frame = _Frames[_Frames.Count - 1];
        _Frames.RemoveAt(_Frames.Count - 1);

        if (frame.LastRootReceivesSplice)
          EnqueueSlotParentVisit(frame.ForestSlot);      // the slot-owning root completed
      }
    }

    private void EnqueueSlotParentVisit(Slot slot)
    {
      if (!slot.HasParent)
        return;

      _Pending.Enqueue(new Emission
      {
        Node = slot.Parent,
        Mode = TreenumeratorMode.VisitingNode,
        VisitCount = ++slot.ParentVisitCount,
        Position = new NodePosition(slot.ParentSiblingIndex, slot.Depth - 1),
      });
    }

    private void Publish(Emission emission)
    {
      Node = emission.Node;
      Position = emission.Position;
      VisitCount = emission.VisitCount;
      Mode = emission.Mode;

      if (emission.Mode == TreenumeratorMode.SchedulingNode)
        _LastScheduledFrame = emission.ScheduledBy;
    }

    protected override async ValueTask OnDisposingAsync()
    {
      await base.OnDisposingAsync().ConfigureAwait(false);

      foreach (var frame in _Frames)
        if (frame.Forest != null)
          await frame.Forest.DisposeAsync().ConfigureAwait(false);

      _Frames.Clear();

      await _Source.DisposeAsync().ConfigureAwait(false);
    }
  }
}
