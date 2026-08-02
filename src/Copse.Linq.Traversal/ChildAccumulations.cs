using System.Collections.Generic;

namespace Copse.Linq
{
  // A no-copy, no-allocation view over a node's children's accumulated values, handed to a
  // LeaffixDispatch survey (the sibling-complete tier; the fold-shaped tier is LeaffixScan,
  // which never needs this view). In the flat pre-order result a node's children sit at
  // scattered indices (each child's subtree is a contiguous span), so this hops them on demand
  // rather than gathering into a temporary array.
  //
  // Deliberately NOT IEnumerable<T>: foreach binds to the public struct Enumerator by pattern
  // (zero allocation), while every interface path (LINQ's Sum, string.Join, ...) would box the
  // view AND its enumerator on every survey -- the silent per-node allocation this library
  // exists to avoid. Dropping the interface makes that cost unrepresentable rather than
  // documented-and-hoped-against.
  public readonly struct ChildAccumulations<TAccumulate>
  {
    internal ChildAccumulations(
      List<TAccumulate> accumulations,
      List<int> subtreeSizes,
      int parentIndex)
    {
      _Accumulations = accumulations;
      _SubtreeSizes = subtreeSizes;
      _ParentIndex = parentIndex;
    }

    private readonly List<TAccumulate> _Accumulations;
    private readonly List<int> _SubtreeSizes;
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
      new Enumerator(_Accumulations, _SubtreeSizes, _ParentIndex);

    public struct Enumerator
    {
      internal Enumerator(List<TAccumulate> accumulations, List<int> subtreeSizes, int parentIndex)
      {
        _Accumulations = accumulations;
        _SubtreeSizes = subtreeSizes;
        _End = parentIndex + subtreeSizes[parentIndex];
        _Next = parentIndex + 1;
        _Cursor = -1;
      }

      private readonly List<TAccumulate> _Accumulations;
      private readonly List<int> _SubtreeSizes;
      private readonly int _End;
      private int _Next;
      private int _Cursor;

      public TAccumulate Current => _Accumulations[_Cursor];

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
