using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Dags.Tests
{
  // The walker law suites' provider fan-out (the tree family's WalkerLawProviders, dualized):
  // the comonad laws must hold for every citizen claiming the walkable contract, not just the
  // buffer -- so each law runs over the buffer (dense ordinals, the CSR skeleton), the buffer
  // transposed twice (the same content through the reversal and back: a distinct object whose
  // ordinals were permuted and restored), and the SKELETON-DIRECT topology: a walkable whose
  // only substance is a raw out-CSR extracted through the public surface and validity-checked
  // on the way in (DagSkeletonValidity), with the in-adjacency derived in the test itself. The
  // skeleton is a lawful carrier representation, not an implementation detail -- admitting it
  // certifies the CSR schedules as extends. Citizens with other handle types (the builder with
  // DagNode handles, the foreign provider with string handles) join through the generic law
  // bodies, since a fan-out list must share a handle type.
  internal static class DagWalkerLawProviders
  {
    public static IEnumerable<(string Name, IWalkableDagnumerable<string, int, decimal> Walkable)> IntHandled(Func<Dag<string, decimal>> factory)
    {
      yield return ("buffer", factory().Materialize());
      yield return ("buffer^TT", factory().Transpose().Transpose());
      yield return ("skeletonDirect", SkeletonDirect(factory()));
    }

    // The raw arrays, rewrapped with nothing else: values in topological order, the out-CSR
    // in dispatch order -- both read off the public stream -- then the validity predicate, then
    // a test-owned topology over the SAME arrays and the Walk adapter for the stream half.
    public static IWalkableDagnumerable<string, int, decimal> SkeletonDirect(Dag<string, decimal> dag)
    {
      var values = ((IDagnumerable<string, decimal>)dag).GetTopologicalOrder().ToArray();
      var ordinalByValue = new Dictionary<string, int>();
      for (var ordinal = 0; ordinal < values.Length; ordinal++)
        ordinalByValue.Add(values[ordinal], ordinal);

      var outEdges = new List<(int Target, decimal Payload)>[values.Length];
      for (var ordinal = 0; ordinal < values.Length; ordinal++)
        outEdges[ordinal] = new List<(int, decimal)>();
      foreach (var edge in dag.GetEdges())
        outEdges[ordinalByValue[edge.Parent]].Add((ordinalByValue[edge.Child], edge.Edge));

      var outOffsets = new int[values.Length + 1];
      for (var ordinal = 0; ordinal < values.Length; ordinal++)
        outOffsets[ordinal + 1] = outOffsets[ordinal] + outEdges[ordinal].Count;
      var outTargets = new int[outOffsets[values.Length]];
      var outPayloads = new decimal[outTargets.Length];
      var slot = 0;
      for (var ordinal = 0; ordinal < values.Length; ordinal++)
        foreach (var (target, payload) in outEdges[ordinal])
        {
          outTargets[slot] = target;
          outPayloads[slot] = payload;
          slot++;
        }

      DagSkeletonValidity.AssertValid(values.Length, outOffsets, outTargets);

      return new TopologyBackedWalkable<string, int, decimal>(new CsrDagTopology(values, outOffsets, outTargets, outPayloads));
    }
  }

  // A walkable that is nothing but a topology plus the Walk adapter -- the shape every
  // third-party provider takes (the open-ecosystem story, in the family's own tests).
  internal sealed class TopologyBackedWalkable<TValue, THandle, TEdge> : IWalkableDagnumerable<TValue, THandle, TEdge>
  {
    public TopologyBackedWalkable(IDagTopology<TValue, THandle, TEdge> topology)
    {
      _Topology = topology;
      _Stream = Dag.FromTopology(topology);
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Topology;
    private readonly IDagnumerable<TValue, TEdge> _Stream;

    public IDagnumerator<TValue, TEdge> GetDagnumerator() => _Stream.GetDagnumerator();

    public DagWalker<TValue, THandle, TEdge> GetDagWalker() => new DagWalker<TValue, THandle, TEdge>(_Topology);
  }

  // The skeleton-direct topology: out-CSR as given, in-adjacency derived by the counting fill
  // (parents in ordinal order, which is discovery order on a topological CSR), sources = the
  // in-degree-zero ordinals. Transpose consistency is asserted at construction.
  internal sealed class CsrDagTopology : IDagTopology<string, int, decimal>
  {
    public CsrDagTopology(string[] values, int[] outOffsets, int[] outTargets, decimal[] outPayloads)
    {
      _Values = values;
      _OutOffsets = outOffsets;
      _OutTargets = outTargets;
      _OutPayloads = outPayloads;

      var nodeCount = values.Length;
      _InOffsets = new int[nodeCount + 1];
      for (var slot = 0; slot < outTargets.Length; slot++)
        _InOffsets[outTargets[slot] + 1]++;
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        _InOffsets[ordinal + 1] += _InOffsets[ordinal];

      _InParents = new int[outTargets.Length];
      _InPayloads = new decimal[outTargets.Length];
      var cursor = new int[nodeCount];
      for (var parent = 0; parent < nodeCount; parent++)
        for (var slot = outOffsets[parent]; slot < outOffsets[parent + 1]; slot++)
        {
          var child = outTargets[slot];
          var fillSlot = _InOffsets[child] + cursor[child]++;
          _InParents[fillSlot] = parent;
          _InPayloads[fillSlot] = outPayloads[slot];
        }

      DagSkeletonValidity.AssertTransposeConsistent(nodeCount, outOffsets, outTargets, _InOffsets, _InParents);

      _Sources = Enumerable.Range(0, nodeCount).Where(ordinal => _InOffsets[ordinal + 1] == _InOffsets[ordinal]).ToArray();
    }

    private readonly string[] _Values;
    private readonly int[] _OutOffsets;
    private readonly int[] _OutTargets;
    private readonly decimal[] _OutPayloads;
    private readonly int[] _InOffsets;
    private readonly int[] _InParents;
    private readonly decimal[] _InPayloads;
    private readonly int[] _Sources;

    public string GetValue(int handle) => _Values[handle];

    public DagStep<int, decimal> TryGetParentAt(int handle, int inEdgeIndex)
      => inEdgeIndex >= 0 && inEdgeIndex < _InOffsets[handle + 1] - _InOffsets[handle]
        ? new DagStep<int, decimal>(_InParents[_InOffsets[handle] + inEdgeIndex], _InPayloads[_InOffsets[handle] + inEdgeIndex], inEdgeIndex)
        : default;

    public DagStep<int, decimal> TryGetChildAt(int handle, int outEdgeIndex)
      => outEdgeIndex >= 0 && outEdgeIndex < _OutOffsets[handle + 1] - _OutOffsets[handle]
        ? new DagStep<int, decimal>(_OutTargets[_OutOffsets[handle] + outEdgeIndex], _OutPayloads[_OutOffsets[handle] + outEdgeIndex], outEdgeIndex)
        : default;

    public DagStep<int, decimal> TryGetSourceAt(int sourceIndex)
      => sourceIndex >= 0 && sourceIndex < _Sources.Length
        ? new DagStep<int, decimal>(_Sources[sourceIndex], default, sourceIndex)
        : default;
  }

  // The provider-mint citizen: the diamond implemented ENTIRELY outside the family over its own
  // native adjacency -- two dictionaries, string handles, no ordinals -- minting walkers through
  // the public DagWalker constructor and streaming through Dag.FromTopology. Copse.Dags grants
  // no InternalsVisibleTo to anyone, so this compiling is the proof that the contract is
  // third-party-implementable.
  internal sealed class FamilyFreeDag : IWalkableDagnumerable<string, string, decimal>, IDagTopology<string, string, decimal>
  {
    private static readonly Dictionary<string, (string Node, decimal Edge)[]> Children = new Dictionary<string, (string, decimal)[]>
    {
      ["apex"] = new[] { ("left", 0.60m), ("right", 0.40m) },
      ["left"] = new[] { ("venture", 0.70m) },
      ["right"] = new[] { ("venture", 0.30m) },
      ["venture"] = new (string, decimal)[0],
    };

    private static readonly Dictionary<string, (string Node, decimal Edge)[]> Parents = new Dictionary<string, (string, decimal)[]>
    {
      ["apex"] = new (string, decimal)[0],
      ["left"] = new[] { ("apex", 0.60m) },
      ["right"] = new[] { ("apex", 0.40m) },
      ["venture"] = new[] { ("left", 0.70m), ("right", 0.30m) },
    };

    private readonly IDagnumerable<string, decimal> _Streaming;

    public FamilyFreeDag()
    {
      _Streaming = Dag.FromTopology(this);
    }

    public IDagnumerator<string, decimal> GetDagnumerator() => _Streaming.GetDagnumerator();

    public DagWalker<string, string, decimal> GetDagWalker() => new DagWalker<string, string, decimal>(this);

    public string GetValue(string handle) => handle;

    public DagStep<string, decimal> TryGetParentAt(string handle, int inEdgeIndex)
    {
      var parents = Parents[handle];
      return inEdgeIndex >= 0 && inEdgeIndex < parents.Length
        ? new DagStep<string, decimal>(parents[inEdgeIndex].Node, parents[inEdgeIndex].Edge, inEdgeIndex)
        : default;
    }

    public DagStep<string, decimal> TryGetChildAt(string handle, int outEdgeIndex)
    {
      var children = Children[handle];
      return outEdgeIndex >= 0 && outEdgeIndex < children.Length
        ? new DagStep<string, decimal>(children[outEdgeIndex].Node, children[outEdgeIndex].Edge, outEdgeIndex)
        : default;
    }

    public DagStep<string, decimal> TryGetSourceAt(int sourceIndex)
      => sourceIndex == 0 ? new DagStep<string, decimal>("apex", default, 0) : default;
  }
}
