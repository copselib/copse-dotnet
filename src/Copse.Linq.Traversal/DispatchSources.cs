using Copse;
using Copse.Core;
using System;

namespace Copse.Linq
{
  /// <summary>
  /// One child of a surveyed node, read-side: its context and its completed accumulation --
  /// DispatchTarget's dual (design-docs/SCANRESULT_DESIGN.md): where the rootfix survey WRITES what
  /// flows down through a target, the leaffix survey READS what flowed up through a source.
  /// Carries the full <see cref="Context"/> per the position ruling: callback-context types
  /// carry coordinates (immediate, consumed in place, never stale); traveling values do not.
  /// </summary>
  public readonly struct DispatchSource<TSource, TAccumulate>
  {
    internal DispatchSource(NodeContext<TSource> context, TAccumulate accumulate)
    {
      Context = context;
      Accumulate = accumulate;
    }

    /// <summary>The child's source value and position.</summary>
    public readonly NodeContext<TSource> Context;

    /// <summary>The child's completed accumulation.</summary>
    public readonly TAccumulate Accumulate;

    /// <summary>The child's source value (shorthand for <c>Context.Node</c>).</summary>
    public TSource Node => Context.Node;

    public override string ToString() => $"{Context} <- {Accumulate}";
  }

  // A no-copy, no-allocation view over a surveyed node's children as read-handles, handed to a
  // LeaffixDispatch survey -- DispatchTargets' dual (the duality audit's write/read pair).
  // Backed by the builds' shared child-index (DispatchChildIndex -- CSR over the preorder
  // encoding), so Count and the indexer are honestly O(1) and enumeration is a flat slice walk.
  //
  // Deliberately NOT IEnumerable<T> / IReadOnlyList<T>: foreach binds to the public struct
  // Enumerator by pattern (zero allocation), while every interface path would box the view AND
  // its enumerator on every survey. ToArray() is the explicit bridge to interface-shaped APIs.
  public readonly struct DispatchSources<TSource, TAccumulate>
  {
    internal DispatchSources(
      TSource[] values,
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

    private readonly TSource[] _Values;
    private readonly NodePosition[] _Positions;
    private readonly int[] _ChildIndices;
    private readonly int[] _ChildOffsets;
    private readonly TAccumulate[] _Accumulations;
    private readonly int _ParentIndex;

    /// <summary>The number of children. O(1) -- read off the child-index offsets.</summary>
    public int Count => _ChildOffsets[_ParentIndex + 1] - _ChildOffsets[_ParentIndex];

    /// <summary>The child's read-handle by sibling index. O(1).</summary>
    public DispatchSource<TSource, TAccumulate> this[int index]
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
        return new DispatchSource<TSource, TAccumulate>(
          new NodeContext<TSource>(_Values[childIndex], _Positions[childIndex]), _Accumulations[childIndex]);
      }
    }

    /// <summary>
    /// The explicit bridge to interface-shaped APIs (LINQ, <c>IEnumerable</c> parameters):
    /// ONE allocation, visible at the call site; the foreach and indexer paths stay
    /// allocation-free.
    /// </summary>
    public DispatchSource<TSource, TAccumulate>[] ToArray()
    {
      var sources = new DispatchSource<TSource, TAccumulate>[Count];

      for (var index = 0; index < sources.Length; index++)
        sources[index] = this[index];

      return sources;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    public struct Enumerator
    {
      internal Enumerator(in DispatchSources<TSource, TAccumulate> sources)
      {
        _Sources = sources;
        _Index = -1;
      }

      private readonly DispatchSources<TSource, TAccumulate> _Sources;
      private int _Index;

      public DispatchSource<TSource, TAccumulate> Current => _Sources[_Index];

      public bool MoveNext() => ++_Index < _Sources.Count;
    }
  }
}
