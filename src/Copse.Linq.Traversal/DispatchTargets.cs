using Copse;
using System.Collections.Generic;

namespace Copse.Linq
{
  // A no-copy, no-allocation view over a surveyed node's children as write-handles, handed to a
  // RootfixDispatch survey. In the flat pre-order encoding a node's children sit at scattered
  // indices (each child's subtree is a contiguous span), so this hops them on demand rather than
  // gathering into a temporary list.
  //
  // Deliberately NOT IEnumerable<T> / IReadOnlyList<T>: foreach binds to the public struct
  // Enumerator by pattern (zero allocation), while every interface path would box the view AND
  // its enumerator on every survey -- the silent per-node allocation this library exists to
  // avoid. A rule that needs a specific sibling (say, the last) makes one pass to find it and a
  // second to dispatch; both are span hops over already-built arrays.
  public readonly struct DispatchTargets<TSource, TDispatch>
  {
    internal DispatchTargets(
      List<NodeContext<TSource>> contexts,
      List<int> subtreeSizes,
      TDispatch[] arrivals,
      bool[] written,
      int parentIndex)
    {
      _Contexts = contexts;
      _SubtreeSizes = subtreeSizes;
      _Arrivals = arrivals;
      _Written = written;
      _ParentIndex = parentIndex;
    }

    private readonly List<NodeContext<TSource>> _Contexts;
    private readonly List<int> _SubtreeSizes;
    private readonly TDispatch[] _Arrivals;
    private readonly bool[] _Written;
    private readonly int _ParentIndex;

    /// <summary>The number of children. O(children) -- counted by hopping the subtree spans.</summary>
    public int Count
    {
      get
      {
        var count = 0;
        var end = _ParentIndex + _SubtreeSizes[_ParentIndex];
        for (var i = _ParentIndex + 1; i < end; i += _SubtreeSizes[i])
          count++;
        return count;
      }
    }

    public Enumerator GetEnumerator() =>
      new Enumerator(_Contexts, _SubtreeSizes, _Arrivals, _Written, _ParentIndex);

    public struct Enumerator
    {
      internal Enumerator(
        List<NodeContext<TSource>> contexts,
        List<int> subtreeSizes,
        TDispatch[] arrivals,
        bool[] written,
        int parentIndex)
      {
        _Contexts = contexts;
        _SubtreeSizes = subtreeSizes;
        _Arrivals = arrivals;
        _Written = written;
        _End = parentIndex + subtreeSizes[parentIndex];
        _Next = parentIndex + 1;
        _Cursor = -1;
      }

      private readonly List<NodeContext<TSource>> _Contexts;
      private readonly List<int> _SubtreeSizes;
      private readonly TDispatch[] _Arrivals;
      private readonly bool[] _Written;
      private readonly int _End;
      private int _Next;
      private int _Cursor;

      public DispatchTarget<TSource, TDispatch> Current =>
        new DispatchTarget<TSource, TDispatch>(_Contexts[_Cursor], _Arrivals, _Written, _Cursor);

      public bool MoveNext()
      {
        if (_Next >= _End)
          return false;

        _Cursor = _Next;
        _Next += _SubtreeSizes[_Cursor]; // hop over this child's whole subtree to the next child
        return true;
      }
    }
  }
}
