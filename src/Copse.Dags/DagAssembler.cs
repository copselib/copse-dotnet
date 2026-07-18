using System.Collections.Generic;

namespace Copse.Dags
{
  // The downward passes' shared result builder: collects nodes at entries, sources and edges at
  // discoveries (per-parent arrival order IS the source's dispatch order, preserving each
  // parent's out-edge order in the rebuilt dag), and wires everything once the pass completes --
  // edges cannot wire during the pass because a child's node exists only from its entry.
  // Ordinals are correlation keys, not indices (wrapped sources carry gaps), hence dictionaries.
  internal sealed class DagAssembler<TResult, TEdge>
  {
    private readonly Dictionary<int, DagNode<TResult, TEdge>> _NodesByOrdinal = new();
    private readonly List<int> _SourceOrdinals = new();
    private readonly List<(int ParentOrdinal, int ChildOrdinal, TEdge Edge)> _Edges = new();

    public void AddSource(int ordinal) => _SourceOrdinals.Add(ordinal);

    public void AddEdge(int parentOrdinal, int childOrdinal, TEdge edge) =>
      _Edges.Add((parentOrdinal, childOrdinal, edge));

    public void AddNode(int ordinal, TResult value) =>
      _NodesByOrdinal.Add(ordinal, new DagNode<TResult, TEdge>(value));

    public Dag<TResult, TEdge> Build()
    {
      foreach (var (parentOrdinal, childOrdinal, edge) in _Edges)
        _NodesByOrdinal[parentOrdinal].AddChild(_NodesByOrdinal[childOrdinal], edge);

      var roots = new List<DagNode<TResult, TEdge>>(_SourceOrdinals.Count);
      foreach (var sourceOrdinal in _SourceOrdinals)
        roots.Add(_NodesByOrdinal[sourceOrdinal]);

      return new Dag<TResult, TEdge>(roots);
    }
  }
}
