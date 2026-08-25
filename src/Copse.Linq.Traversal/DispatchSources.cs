using Copse;
using Copse.Core;
using System;

namespace Copse.Linq
{
  /// <summary>
  /// One child of a surveyed node, read-side: its context and its completed accumulation.
  /// The counterpart of <c>DispatchTarget</c> -- where a downward survey writes into its
  /// targets, an upward survey reads from its sources.
  /// </summary>
  public readonly struct DispatchSource<TNode, TAccumulate>
  {
    internal DispatchSource(NodeContext<TNode> context, TAccumulate accumulate)
    {
      Context = context;
      Accumulate = accumulate;
    }

    /// <summary>The child's source value and position.</summary>
    public readonly NodeContext<TNode> Context;

    /// <summary>The child's completed accumulation.</summary>
    public readonly TAccumulate Accumulate;

    /// <summary>The child's source value (shorthand for <c>Context.Node</c>).</summary>
    public TNode Node => Context.Node;

    public override string ToString() => $"{Context} <- {Accumulate}";
  }

  // Backed by the builds' shared child-index (DispatchChildIndex -- CSR over the preorder
  // encoding). Deliberately NOT IEnumerable<T> / IReadOnlyList<T>: foreach binds to the public
  // struct Enumerator by pattern (zero allocation), while every interface path would box the
  // view AND its enumerator on every survey.
  /// <summary>
  /// The view a <c>LeaffixDispatch</c> survey receives: all of the surveyed node's children at
  /// once, each with its completed accumulation. <see cref="Count"/> and the indexer are O(1),
  /// and <c>foreach</c> over the view allocates nothing; <see cref="ToArray"/> is the bridge
  /// when an API needs an <c>IEnumerable</c>.
  /// </summary>
  public readonly struct DispatchSources<TNode, TAccumulate>
  {
    internal DispatchSources(
      TNode[] values,
      NodePosition[] positions,
      int[] childIndices,
      int[] childOffsets,
      TAccumulate[] accumulations,
      int parentIndex)
    {
      _Values = values;
      _Positions = positions;
      _ChildIndices = childIndices;
      _ChildOffsets = childOffsets;
      _Accumulations = accumulations;
      _ParentIndex = parentIndex;
    }

    private readonly TNode[] _Values;
    private readonly NodePosition[] _Positions;
    private readonly int[] _ChildIndices;
    private readonly int[] _ChildOffsets;
    private readonly TAccumulate[] _Accumulations;
    private readonly int _ParentIndex;

    /// <summary>The number of children. O(1) -- read off the child-index offsets.</summary>
    public int Count => _ChildOffsets[_ParentIndex + 1] - _ChildOffsets[_ParentIndex];

    /// <summary>The child's read-handle by sibling index. O(1).</summary>
    public DispatchSource<TNode, TAccumulate> this[int index]
    {
      get
      {
        if ((uint)index >= (uint)Count)
          throw new ArgumentOutOfRangeException(
            nameof(index), $"Sibling index {index} is outside the surveyed node's {Count} children.");

        var childIndex = _ChildIndices[_ChildOffsets[_ParentIndex] + index];

        // Positions come from the child-index build's exact-size array (no capture side
        // channel) -- the reverse fold cannot derive them statelessly the way the rootfix
        // pass does, and a close-stack walk measured out (O(n) entries on chains).
        return new DispatchSource<TNode, TAccumulate>(
          new NodeContext<TNode>(_Values[childIndex], _Positions[childIndex]), _Accumulations[childIndex]);
      }
    }

    /// <summary>
    /// The explicit bridge to interface-shaped APIs (LINQ, <c>IEnumerable</c> parameters):
    /// ONE allocation, visible at the call site; the foreach and indexer paths stay
    /// allocation-free.
    /// </summary>
    public DispatchSource<TNode, TAccumulate>[] ToArray()
    {
      var sources = new DispatchSource<TNode, TAccumulate>[Count];

      for (var index = 0; index < sources.Length; index++)
        sources[index] = this[index];

      return sources;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    public struct Enumerator
    {
      internal Enumerator(in DispatchSources<TNode, TAccumulate> sources)
      {
        _Sources = sources;
        _Index = -1;
      }

      private readonly DispatchSources<TNode, TAccumulate> _Sources;
      private int _Index;

      public DispatchSource<TNode, TAccumulate> Current => _Sources[_Index];

      public bool MoveNext() => ++_Index < _Sources.Count;
    }
  }
}
