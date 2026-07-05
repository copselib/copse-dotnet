using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // A tree implemented directly against the ITreenumerable/ITreenumerator contract -- no engine,
  // no child enumerators, no wrapper bases. It stands in for "somebody else's implementation"
  // (a database cursor, a REST adapter, ...): the conformance tests prove the operator suite
  // works over ANY implementation honoring the visit-stream contract, and this file doubles as
  // an executable, engine-independent specification of that contract.
  //
  // The semantics were transcribed from the engine (DepthFirstTreenumerator/DepthFirstPath and
  // BreadthFirstTreenumerator/BreadthFirstPath) and are locked to it by
  // ContractTreeConformanceTests. The two load-bearing subtleties:
  //  - DFT: a backtracked-to accepted node is owed a between/after-children visit only if
  //    something below it was VISITED since its last visit (the depth-of-last-visited rule);
  //  - BFT: an accepted front is revisited once after every child SLOT that enqueued at least
  //    one accepted node (a bool carry, consumed once per slot).
  // In both dimensions a consumer-SkipNode'd node emits no visits of its own while its children
  // are traversed in its place at their RAW positions (no depth compression, no renumbering --
  // that is Where's job, not the traversal contract's).
  internal sealed class ContractTree : ITreenumerable<string>
  {
    private ContractTree(string[] values, int[][] children, int[] roots)
    {
      _Values = values;
      _Children = children;
      _Roots = roots;
    }

    private readonly string[] _Values;
    private readonly int[][] _Children;
    private readonly int[] _Roots;

    // Same grammar as TreeSerializer: values are runs of non-structural characters; a value
    // followed by '(' is a parent; ',' separates siblings.
    public static ContractTree Parse(string text)
    {
      var values = new List<string>();
      var children = new List<List<int>>();
      var roots = new List<int>();
      var open = new Stack<int>();
      var valueStart = -1;

      int Commit(int end)
      {
        var index = values.Count;
        values.Add(text.Substring(valueStart, end - valueStart));
        children.Add(new List<int>());

        if (open.Count == 0)
          roots.Add(index);
        else
          children[open.Peek()].Add(index);

        valueStart = -1;
        return index;
      }

      for (var i = 0; i < text.Length; i++)
      {
        switch (text[i])
        {
          case '(':
            open.Push(Commit(i));
            break;

          case ')':
            if (valueStart >= 0)
              Commit(i);
            open.Pop();
            break;

          case ',':
            if (valueStart >= 0)
              Commit(i);
            break;

          default:
            if (valueStart < 0)
              valueStart = i;
            break;
        }
      }

      if (valueStart >= 0)
        Commit(text.Length);

      var childArrays = new int[children.Count][];
      for (var i = 0; i < children.Count; i++)
        childArrays[i] = children[i].ToArray();

      return new ContractTree(values.ToArray(), childArrays, roots.ToArray());
    }

    public ITreenumerator<string> GetDepthFirstTreenumerator()
      => new DepthFirstContractTreenumerator(this);

    public ITreenumerator<string> GetBreadthFirstTreenumerator()
      => new BreadthFirstContractTreenumerator(this);

    private sealed class Frame
    {
      public int NodeIndex;
      public NodePosition Position;
      public int VisitCount;
      public bool Accepted = true;
      public int ChildCursor;
    }

    private sealed class DepthFirstContractTreenumerator : ITreenumerator<string>
    {
      public DepthFirstContractTreenumerator(ContractTree tree)
      {
        _Tree = tree;
      }

      private readonly ContractTree _Tree;
      private readonly List<Frame> _Path = new List<Frame>();
      private int _NextRoot;
      private bool _RootsFinished;
      private int _DepthOfLastVisitedNode = -1;
      private bool _Finished;
      private bool _Disposed;

      public string Node { get; private set; }
      public int VisitCount { get; private set; }
      public TreenumeratorMode Mode { get; private set; }
      // CONTRACT CLAUSE (discovered via WhereDepthFirstTreenumerator's sentinel, which snapshots
      // the inner Position at construction): the pre-enumeration position is (0, -1) -- depth -1,
      // "above the roots" -- matching TreenumeratorBase's initializer. default(NodePosition) is
      // (0, 0), which reads as an already-scheduled root and desyncs wrappers.
      public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

      public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies)
      {
        if (_Disposed || _Finished)
          return false;

        // Nothing scheduled yet: the very first move ignores the strategy.
        if (_Path.Count == 0)
          return MoveToNextRoot();

        // The strategy applies to the node just scheduled; visiting nodes ignore it.
        if (Mode == TreenumeratorMode.SchedulingNode)
          return OnScheduling(nodeTraversalStrategies);

        return OnVisiting();
      }

      private bool OnScheduling(NodeTraversalStrategies nodeTraversalStrategies)
      {
        var top = _Path[_Path.Count - 1];

        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
          SkipRemainingSiblings();

        // SkipNodeAndDescendants is a superset of SkipNode; check it first.
        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
          return Backtrack();

        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        {
          // Swallow the node (it emits no visits); its children are traversed in its place.
          top.Accepted = false;

          if (TryPushNextChild())
            return true;

          return Backtrack();
        }

        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
          top.ChildCursor = int.MaxValue;

        // Accept: emit the node's first visit.
        top.VisitCount = 1;
        _DepthOfLastVisitedNode = top.Position.Depth;
        Publish(top);
        return true;
      }

      private bool OnVisiting()
      {
        if (TryPushNextChild())
          return true;

        return Backtrack();
      }

      private bool Backtrack()
      {
        while (true)
        {
          _Path.RemoveAt(_Path.Count - 1);

          if (_Path.Count == 0)
            return MoveToNextRoot();

          var frame = _Path[_Path.Count - 1];

          // No visit owed here: the node already took its return visit (nothing below was visited
          // since), or it is a SkipNode'd level. Promote its next child instead.
          if (frame.Position.Depth == _DepthOfLastVisitedNode || !frame.Accepted)
          {
            if (TryPushNextChild())
              return true;

            continue;
          }

          // The accepted node here owes its next between/after-children visit.
          frame.VisitCount++;
          _DepthOfLastVisitedNode = frame.Position.Depth;
          Publish(frame);
          return true;
        }
      }

      private bool MoveToNextRoot()
      {
        if (_RootsFinished || _NextRoot >= _Tree._Roots.Length)
        {
          _Finished = true;
          return false;
        }

        var frame = new Frame
        {
          NodeIndex = _Tree._Roots[_NextRoot],
          Position = new NodePosition(_NextRoot, 0),
        };

        _NextRoot++;
        _Path.Add(frame);
        Publish(frame);
        return true;
      }

      private bool TryPushNextChild()
      {
        var parent = _Path[_Path.Count - 1];
        var childIndices = _Tree._Children[parent.NodeIndex];

        if (parent.ChildCursor >= childIndices.Length)
          return false;

        var siblingIndex = parent.ChildCursor;
        parent.ChildCursor++;

        var frame = new Frame
        {
          NodeIndex = childIndices[siblingIndex],
          Position = new NodePosition(siblingIndex, parent.Position.Depth + 1),
        };

        _Path.Add(frame);
        Publish(frame);
        return true;
      }

      // Prune every frame below the top down through the nearest accepted ancestor (inclusive:
      // its cursor dying is what silences the node's remaining siblings). If every ancestor is
      // skipped, the node is an effective root and the remaining roots are its siblings.
      private void SkipRemainingSiblings()
      {
        for (var i = _Path.Count - 2; i >= 0; i--)
        {
          var frame = _Path[i];
          var accepted = frame.Accepted;
          frame.ChildCursor = int.MaxValue;

          if (accepted)
            return;
        }

        _RootsFinished = true;
      }

      private void Publish(Frame frame)
      {
        Mode = frame.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
        Node = _Tree._Values[frame.NodeIndex];
        VisitCount = frame.VisitCount;
        Position = frame.Position;
      }

      public void Dispose() => _Disposed = true;
    }

    private sealed class BreadthFirstContractTreenumerator : ITreenumerator<string>
    {
      public BreadthFirstContractTreenumerator(ContractTree tree)
      {
        _Tree = tree;
      }

      private readonly ContractTree _Tree;
      // The node being classified, plus any SkipNode'd ancestors whose children are promoting.
      private readonly List<Frame> _ScheduleStack = new List<Frame>();
      // Accepted nodes, scheduled but not yet fully visited. The front is the active parent.
      private readonly Queue<Frame> _VisitQueue = new Queue<Frame>();
      private int _NextRoot;
      private bool _RootsFinished;
      private bool _RootsScheduled;
      // True when the front's in-progress child slot has enqueued at least one accepted node.
      private bool _SlotCarry;
      private bool _Finished;
      private bool _Disposed;

      public string Node { get; private set; }
      public int VisitCount { get; private set; }
      public TreenumeratorMode Mode { get; private set; }
      // CONTRACT CLAUSE (discovered via WhereDepthFirstTreenumerator's sentinel, which snapshots
      // the inner Position at construction): the pre-enumeration position is (0, -1) -- depth -1,
      // "above the roots" -- matching TreenumeratorBase's initializer. default(NodePosition) is
      // (0, 0), which reads as an already-scheduled root and desyncs wrappers.
      public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

      public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies)
      {
        if (_Disposed || _Finished)
          return false;

        // A strategy only applies to the node just scheduled; visiting nodes ignore it.
        if (Mode == TreenumeratorMode.SchedulingNode && _ScheduleStack.Count > 0)
          ApplyStrategy(nodeTraversalStrategies);

        return Advance();
      }

      private void ApplyStrategy(NodeTraversalStrategies nodeTraversalStrategies)
      {
        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        {
          // Silence the skipped ancestors' remaining children...
          for (var i = 0; i < _ScheduleStack.Count - 1; i++)
            _ScheduleStack[i].ChildCursor = int.MaxValue;

          // ...then the nearest accepted ancestor's. When every ancestor is skipped the node is
          // an effective root and its siblings are the remaining roots.
          if (_ScheduleStack[_ScheduleStack.Count - 1].Position.Depth == _ScheduleStack.Count - 1)
            _RootsFinished = true;
          else
            _VisitQueue.Peek().ChildCursor = int.MaxValue;
        }

        // SkipNodeAndDescendants is a superset of SkipNode; check it first.
        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        {
          // Erase the node and its subtree; the slot enqueues nothing.
          _ScheduleStack.RemoveAt(_ScheduleStack.Count - 1);
          return;
        }

        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
          // Keep the node resident so Advance can promote its children into its slot.
          return;

        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
          _ScheduleStack[_ScheduleStack.Count - 1].ChildCursor = int.MaxValue;

        // Accept: move the node onto the visit queue and record that this slot enqueued.
        var frame = _ScheduleStack[_ScheduleStack.Count - 1];
        _ScheduleStack.RemoveAt(_ScheduleStack.Count - 1);
        _VisitQueue.Enqueue(frame);
        _SlotCarry = true;
      }

      private bool Advance()
      {
        while (true)
        {
          // 1) Descend: schedule the next child of the schedule-stack top (a node awaiting
          //    classification, or a SkipNode'd node whose children promote into its slot).
          if (_ScheduleStack.Count > 0)
          {
            if (TryScheduleNextChildOf(_ScheduleStack[_ScheduleStack.Count - 1]))
              return true;

            _ScheduleStack.RemoveAt(_ScheduleStack.Count - 1);
            continue;
          }

          // 2) Schedule the next root (the forest's children -- no surrounding visits).
          if (!_RootsScheduled)
          {
            if (TryScheduleNextRoot())
              return true;

            _RootsScheduled = true;
            // Enqueues made while scheduling roots have no owing parent; clear the carry.
            _SlotCarry = false;
            continue;
          }

          if (_VisitQueue.Count == 0)
          {
            _Finished = true;
            return false;
          }

          // 3) Visit the active parent and drive its children.
          var front = _VisitQueue.Peek();

          if (front.VisitCount == 0)
          {
            front.VisitCount = 1;
            Publish(front);
            return true;
          }

          if (_SlotCarry)
          {
            // The slot that just finished enqueued at least one node: the parent is visited.
            _SlotCarry = false;
            front.VisitCount++;
            Publish(front);
            return true;
          }

          if (TryScheduleNextChildOf(front))
            return true;

          // The parent has no more children: retire it. The next turn visits the new front.
          _VisitQueue.Dequeue();
        }
      }

      private bool TryScheduleNextChildOf(Frame parent)
      {
        var childIndices = _Tree._Children[parent.NodeIndex];

        if (parent.ChildCursor >= childIndices.Length)
          return false;

        var siblingIndex = parent.ChildCursor;
        parent.ChildCursor++;

        var frame = new Frame
        {
          NodeIndex = childIndices[siblingIndex],
          Position = new NodePosition(siblingIndex, parent.Position.Depth + 1),
        };

        _ScheduleStack.Add(frame);
        Publish(frame);
        return true;
      }

      private bool TryScheduleNextRoot()
      {
        if (_RootsFinished || _NextRoot >= _Tree._Roots.Length)
          return false;

        var frame = new Frame
        {
          NodeIndex = _Tree._Roots[_NextRoot],
          Position = new NodePosition(_NextRoot, 0),
        };

        _NextRoot++;
        _ScheduleStack.Add(frame);
        Publish(frame);
        return true;
      }

      private void Publish(Frame frame)
      {
        Mode = frame.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
        Node = _Tree._Values[frame.NodeIndex];
        VisitCount = frame.VisitCount;
        Position = frame.Position;
      }

      public void Dispose() => _Disposed = true;
    }
  }
}
