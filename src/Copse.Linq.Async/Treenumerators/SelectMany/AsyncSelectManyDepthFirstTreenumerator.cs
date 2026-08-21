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
  // are structure only, carried by a stack of FRAMES (one per open source node, so the stack
  // is the open source path and its count is the top node's depth plus one) that says where
  // that node's replacement went and where its children go.
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
  // Slots are a second stack, the root slot at its bottom: a frame that realizes a slot under
  // its last root pushes one, and pops it when it pops. So the top slot is always where the
  // top frame's children go (its own slot, or -- when it realized none -- the slot it was
  // itself emitted into), and when the top frame owns the top slot, the slot beneath is the
  // one its roots went into. Frames and slots are structs addressed by ref; the async methods
  // are thin seams around the awaits because by-reference locals are illegal there (CS8177),
  // and every mutation lives in a synchronous helper.
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
      _Slots.AddLast(new Slot(depth: 0));
    }

    private readonly IAsyncTreenumerator<TSource> _Source;
    private readonly Func<TSource, AsyncExpansion<TResult>> _Selector;
    private readonly RefSemiDeque<Frame> _Frames = new RefSemiDeque<Frame>();
    private readonly RefSemiDeque<Slot> _Slots = new RefSemiDeque<Slot>();
    private readonly Queue<Emission> _Pending = new Queue<Emission>();
    private bool _SourceExhausted;

    // A slot: an output depth, a running sibling index for the roots emitted there, and --
    // when the slot is under a node -- that node, for the visits manufactured after each root.
    private struct Slot
    {
      public Slot(int depth)
      {
        this = default;
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

    private struct Frame
    {
      public Frame(SlotPlacement placement, IAsyncTreenumerator<TResult> forest)
      {
        this = default;
        Placement = placement;
        Forest = forest;
        SkipSpliceCandidateRoot = -1;
      }

      public SlotPlacement Placement;
      public bool SingleValuePending;                   // a one-node forest, scheduled, its visit not yet released
      public TResult SingleValue;
      public IAsyncTreenumerator<TResult> Forest;        // null: no forest, or drained
      public Phase Phase;                                // default: DrainingForest
      public NodeTraversalStrategies ForestStrategies;   // default: TraverseAll
      public bool HasHeld;
      public Emission Held;                              // a forest root's visit, awaiting the next event
      public int CurrentRootSiblingIndex;                // the output index of the forest root now open
      public int SkipSpliceCandidateRoot;                // a root the consumer skipped the descendants of
      public bool OwnsSlot;                              // UnderLastRoot realized on a nonempty forest: this frame pushed a slot
      public bool SkipDescendantsOwed;                   // the next source pull skips the node's subtree
    }

    private struct Emission
    {
      public TResult Node;
      public NodePosition Position;
      public int VisitCount;
      public TreenumeratorMode Mode;
    }

    // Where the top frame's roots went. (Where its children go is the top slot itself.)
    private ref Slot ForestSlot => ref _Slots.GetFromBack(_Frames.GetLast().OwnsSlot ? 1 : 0);

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Consumer strategies bind to the last scheduled node, always a forest node of the top
      // frame (nothing is pulled between a scheduling and the next MoveNext); visits take none.
      if (Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies != NodeTraversalStrategies.TraverseAll
        && _Frames.Count > 0)
        NoteStrategies(nodeTraversalStrategies);

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

        if (_Frames.GetLast().Phase == Phase.DrainingForest)
        {
          await DrainForestAsync().ConfigureAwait(false);
          continue;
        }

        await PullSourceAsync(TakeOwedSourceStrategies()).ConfigureAwait(false);
      }
    }

    private void NoteStrategies(NodeTraversalStrategies strategies)
    {
      ref var frame = ref _Frames.GetLast();

      frame.ForestStrategies = strategies;

      if (Position.Depth == ForestSlot.Depth
        && strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        frame.SkipSpliceCandidateRoot = frame.CurrentRootSiblingIndex;
    }

    private NodeTraversalStrategies TakeOwedSourceStrategies()
    {
      ref var frame = ref _Frames.GetLast();

      var strategies = frame.SkipDescendantsOwed ? NodeTraversalStrategies.SkipDescendants : NodeTraversalStrategies.TraverseAll;
      frame.SkipDescendantsOwed = false;

      return strategies;
    }

    private NodeTraversalStrategies TakeForestStrategies()
    {
      ref var frame = ref _Frames.GetLast();

      var strategies = frame.ForestStrategies;
      frame.ForestStrategies = NodeTraversalStrategies.TraverseAll;

      return strategies;
    }

    private async ValueTask DrainForestAsync()
    {
      if (_Frames.GetLast().SingleValuePending)
      {
        ReleaseSingleValue();
        return;
      }

      var forest = _Frames.GetLast().Forest;

      if (forest == null)
      {
        OnForestDrained();
        return;
      }

      if (!await forest.MoveNextAsync(TakeForestStrategies()).ConfigureAwait(false))
      {
        await forest.DisposeAsync().ConfigureAwait(false);
        _Frames.GetLast().Forest = null;
        OnForestDrained();
        return;
      }

      OnForestEvent(forest);
    }

    private void OnForestEvent(IAsyncTreenumerator<TResult> forest)
    {
      ref var frame = ref _Frames.GetLast();
      ref var slot = ref ForestSlot;

      var isRoot = forest.Position.Depth == 0;
      var startsRoot = isRoot && forest.Mode == TreenumeratorMode.SchedulingNode;

      if (frame.HasHeld)
      {
        frame.HasHeld = false;
        _Pending.Enqueue(frame.Held);

        if (startsRoot)
          EnqueueSlotParentVisit(ref slot);              // the previous root completed
      }

      if (startsRoot)
        frame.CurrentRootSiblingIndex = slot.NextSiblingIndex++;

      var emission = new Emission
      {
        Node = forest.Node,
        Mode = forest.Mode,
        VisitCount = forest.VisitCount,
        Position = new NodePosition(
          isRoot ? frame.CurrentRootSiblingIndex : forest.Position.SiblingIndex,
          slot.Depth + forest.Position.Depth),
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
    private void ScheduleSingleValue(TResult value)
    {
      ref var frame = ref _Frames.GetLast();
      ref var slot = ref ForestSlot;

      frame.CurrentRootSiblingIndex = slot.NextSiblingIndex++;
      frame.SingleValue = value;
      frame.SingleValuePending = true;

      _Pending.Enqueue(new Emission
      {
        Node = value,
        Mode = TreenumeratorMode.SchedulingNode,
        VisitCount = 0,
        Position = new NodePosition(frame.CurrentRootSiblingIndex, slot.Depth),
      });
    }

    private void ReleaseSingleValue()
    {
      ref var frame = ref _Frames.GetLast();

      frame.SingleValuePending = false;

      if (!frame.ForestStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        frame.HasHeld = true;
        frame.Held = new Emission
        {
          Node = frame.SingleValue,
          Mode = TreenumeratorMode.VisitingNode,
          VisitCount = 1,
          Position = new NodePosition(frame.CurrentRootSiblingIndex, ForestSlot.Depth),
        };
      }

      frame.ForestStrategies = NodeTraversalStrategies.TraverseAll;
      OnForestDrained();
    }

    private void OnForestDrained()
    {
      ref var frame = ref _Frames.GetLast();

      if (frame.HasHeld)
      {
        frame.HasHeld = false;
        _Pending.Enqueue(frame.Held);                    // the last root's last visit

        if (frame.Placement == SlotPlacement.UnderLastRoot)
        {
          frame.OwnsSlot = true;
          _Slots.AddLast(new Slot(
            depth: frame.Held.Position.Depth + 1,
            parent: frame.Held.Node,
            parentSiblingIndex: frame.Held.Position.SiblingIndex,
            parentVisitCount: frame.Held.VisitCount,
            nextSiblingIndex: frame.Held.VisitCount - 1));
        }
        else
        {
          EnqueueSlotParentVisit(ref ForestSlot);        // the last root completed
        }
      }

      // Otherwise the children go where the roots went: AfterRoots, None, or UnderLastRoot on
      // nothing -- the top slot, unchanged.

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

      OnSourceEvent();
    }

    private void OnSourceEvent()
    {
      var depth = _Source.Position.Depth;

      if (_Source.Mode == TreenumeratorMode.SchedulingNode)
      {
        PopFramesDeeperThan(depth - 1);

        var expansion = _Selector(_Source.Node);

        _Frames.AddLast(new Frame(
          expansion.Placement,
          expansion.HasSingleValue ? null : expansion.Forest?.GetAsyncDepthFirstTreenumerator()));

        if (expansion.HasSingleValue)
          ScheduleSingleValue(expansion.SingleValue);
      }
      else
      {
        PopFramesDeeperThan(depth);                      // the visit of an open node: structure only
      }
    }

    private void PopFramesDeeperThan(int depth)
    {
      while (_Frames.Count > depth + 1)
      {
        if (_Frames.RemoveLast().OwnsSlot)
        {
          _Slots.RemoveLast();
          EnqueueSlotParentVisit(ref _Slots.GetLast()); // the slot-owning root completed
        }
      }
    }

    private void EnqueueSlotParentVisit(ref Slot slot)
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
    }

    protected override async ValueTask OnDisposingAsync()
    {
      await base.OnDisposingAsync().ConfigureAwait(false);

      while (_Frames.Count > 0)
      {
        var forest = _Frames.RemoveLast().Forest;

        if (forest != null)
          await forest.DisposeAsync().ConfigureAwait(false);
      }

      await _Source.DisposeAsync().ConfigureAwait(false);
    }
  }
}
