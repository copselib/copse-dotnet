using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// The survey tier's pairing -- a DISTINCT type from <see cref="DagScanResult{TNode, TAccumulate}"/>
  /// by design (the recording rule made type-level, 2026-08-05): a survey is the family's
  /// 1-in-n-out shape with no node-grained output, so it records its INPUT -- the node's
  /// complete arrival group -- and the two tiers never overload one field with two meanings.
  /// <see cref="Arrivals"/> is a no-copy view over the result buffer's flat arrival array,
  /// aligned with in-edge order (element i arrived on in-edge i; a source's single element is
  /// the virtual family's delivery). Provenance is NOT carried here -- who dispatched arrival
  /// i is index arithmetic over the buffer's transpose adjacency (the Dispatcher seat's split
  /// homes, 2026-08-05); the callback-time view keeps it.
  /// </summary>
  public readonly struct DagDispatchResult<TNode, TDispatch>
  {
    public DagDispatchResult(TNode node, DagArrivals<TDispatch> arrivals)
    {
      Node = node;
      Arrivals = arrivals;
    }

    public readonly TNode Node;
    public readonly DagArrivals<TDispatch> Arrivals;

    public override string ToString() => $"{Node} <- [{Arrivals.Count} arrivals]";
  }

  /// <summary>
  /// A node's arrival group: a no-copy slice of the pass's flat arrival array, in in-edge
  /// order. Deliberately a struct view with indexer/Count (interface paths would box).
  /// </summary>
  public readonly struct DagArrivals<TDispatch>
  {
    internal DagArrivals(TDispatch[] all, int offset, int count)
    {
      _All = all;
      _Offset = offset;
      Count = count;
    }

    private readonly TDispatch[] _All;
    private readonly int _Offset;

    public int Count { get; }

    public TDispatch this[int index]
    {
      get
      {
        if ((uint)index >= (uint)Count)
          throw new ArgumentOutOfRangeException(nameof(index));
        return _All[_Offset + index];
      }
    }

    /// <summary>The explicit bridge to interface-shaped APIs; the view itself never boxes.</summary>
    public TDispatch[] ToArray()
    {
      var result = new TDispatch[Count];
      Array.Copy(_All, _Offset, result, 0, Count);
      return result;
    }
  }
}
