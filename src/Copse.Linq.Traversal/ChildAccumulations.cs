using System.Collections;
using System.Collections.Generic;

namespace Copse.Linq
{
  // A no-copy, no-allocation view over a node's children's accumulated values, handed to a
  // LeaffixScan accumulator. In the flat pre-order result a node's children sit at scattered
  // indices (each child's subtree is a contiguous span), so this hops them on demand rather than
  // gathering into a temporary array. `foreach` uses the struct Enumerator (zero allocation); the
  // IEnumerable<T> path (e.g. string.Join) boxes the enumerator, so prefer foreach in hot code.
  public readonly struct ChildAccumulations<TAccumulate> : IEnumerable<TAccumulate>
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

    IEnumerator<TAccumulate> IEnumerable<TAccumulate>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<TAccumulate>
    {
      internal Enumerator(List<TAccumulate> accumulations, List<int> subtreeSizes, int parentIndex)
      {
        _Accumulations = accumulations;
        _SubtreeSizes = subtreeSizes;
        _Start = parentIndex + 1;
        _End = parentIndex + subtreeSizes[parentIndex];
        _Next = _Start;
        _Cursor = -1;
      }

      private readonly List<TAccumulate> _Accumulations;
      private readonly List<int> _SubtreeSizes;
      private readonly int _Start;
      private readonly int _End;
      private int _Next;
      private int _Cursor;

      public TAccumulate Current => _Accumulations[_Cursor];
      object IEnumerator.Current => Current;

      public bool MoveNext()
      {
        if (_Next >= _End)
          return false;

        _Cursor = _Next;
        _Next += _SubtreeSizes[_Cursor]; // hop over this child's whole subtree to the next child
        return true;
      }

      public void Reset()
      {
        _Next = _Start;
        _Cursor = -1;
      }

      public void Dispose() { }
    }
  }
}
