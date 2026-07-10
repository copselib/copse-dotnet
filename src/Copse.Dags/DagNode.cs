using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Dags
{
  /// <summary>
  /// An out-edge: the child it points to plus the consumer's per-edge payload (an ownership
  /// fraction, a share class, ...). Edge data lives HERE, on the link, because in a DAG it is a
  /// property of the (parent, child) pair -- a shared child is owned differently by each parent --
  /// and because the library's clones (Select, scans, prunes) must carry it edge-for-edge, which
  /// no value-side convention (e.g. a fractions list aligned with child order) can survive.
  /// </summary>
  public readonly struct DagEdge<TValue, TEdge>
  {
    public DagEdge(DagNode<TValue, TEdge> child, TEdge value)
    {
      Child = child;
      Value = value;
    }

    public readonly DagNode<TValue, TEdge> Child;
    public readonly TEdge Value;
  }

  /// <summary>The maintained back-link view of an edge: the owning parent plus the same payload.</summary>
  public readonly struct DagParentEdge<TValue, TEdge>
  {
    public DagParentEdge(DagNode<TValue, TEdge> parent, TEdge value)
    {
      Parent = parent;
      Value = value;
    }

    public readonly DagNode<TValue, TEdge> Parent;
    public readonly TEdge Value;
  }

  /// <summary>
  /// A node in a DAG. The library owns this reference type so that node identity IS reference
  /// identity: a node is shared by linking the <b>same instance</b> under multiple parents, and
  /// the consumer's <typeparamref name="TValue"/> (and <typeparamref name="TEdge"/>) is never
  /// compared or hashed -- Copse's no-node-equality principle carries over to DAGs structurally
  /// rather than via a comparer.
  ///
  /// <para>Out-edges are an <b>ordered</b> list per parent (sibling order is an edge property, so
  /// a shared child can sit at a different index under each parent), each carrying a
  /// <typeparamref name="TEdge"/> payload, and parallel edges (the same child linked twice under
  /// one parent) are permitted. <see cref="ParentEdges"/> is the maintained back-link: one entry
  /// per in-edge, so it too can contain duplicates.</para>
  ///
  /// <para>Linking is unvalidated for cycles -- acyclicity is enforced when a <see cref="Dag{TValue, TEdge}"/>
  /// operation walks the graph (see <see cref="Dag{TValue, TEdge}.GetTopologicalOrder"/>).</para>
  /// </summary>
  public sealed class DagNode<TValue, TEdge>
  {
    public DagNode(TValue value)
    {
      Value = value;
      Children = new ProjectedListView<DagEdge<TValue, TEdge>, DagNode<TValue, TEdge>>(_ChildEdges, edge => edge.Child);
      Parents = new ProjectedListView<DagParentEdge<TValue, TEdge>, DagNode<TValue, TEdge>>(_ParentEdges, parentEdge => parentEdge.Parent);
    }

    public TValue Value { get; set; }

    private readonly List<DagEdge<TValue, TEdge>> _ChildEdges = new List<DagEdge<TValue, TEdge>>();
    private readonly List<DagParentEdge<TValue, TEdge>> _ParentEdges = new List<DagParentEdge<TValue, TEdge>>();

    public IReadOnlyList<DagEdge<TValue, TEdge>> ChildEdges => _ChildEdges;
    public IReadOnlyList<DagParentEdge<TValue, TEdge>> ParentEdges => _ParentEdges;

    /// <summary>The out-edge targets, a live index-preserving view over <see cref="ChildEdges"/>.</summary>
    public IReadOnlyList<DagNode<TValue, TEdge>> Children { get; }

    /// <summary>The in-edge sources, a live index-preserving view over <see cref="ParentEdges"/>.</summary>
    public IReadOnlyList<DagNode<TValue, TEdge>> Parents { get; }

    /// <summary>
    /// Appends an edge to <paramref name="child"/> carrying <paramref name="edgeValue"/> and
    /// back-links this node into the child's parent edges. Returns the <b>child</b> so freshly
    /// built spines chain downward (<c>root.AddChild(a, w1).AddChild(b, w2)</c> puts b under a).
    /// </summary>
    public DagNode<TValue, TEdge> AddChild(DagNode<TValue, TEdge> child, TEdge edgeValue)
    {
      if (child == null)
        throw new ArgumentNullException(nameof(child));

      _ChildEdges.Add(new DagEdge<TValue, TEdge>(child, edgeValue));
      child._ParentEdges.Add(new DagParentEdge<TValue, TEdge>(this, edgeValue));
      return child;
    }

    /// <summary>As <see cref="AddChild(DagNode{TValue, TEdge}, TEdge)"/> with a default edge payload.</summary>
    public DagNode<TValue, TEdge> AddChild(DagNode<TValue, TEdge> child) => AddChild(child, default);

    /// <summary>Convenience overload: wraps <paramref name="value"/> in a fresh node and links it.</summary>
    public DagNode<TValue, TEdge> AddChild(TValue value, TEdge edgeValue) =>
      AddChild(new DagNode<TValue, TEdge>(value), edgeValue);

    /// <summary>Convenience overload: fresh node, default edge payload.</summary>
    public DagNode<TValue, TEdge> AddChild(TValue value) =>
      AddChild(new DagNode<TValue, TEdge>(value), default);

    /// <summary>
    /// Removes one edge to <paramref name="child"/> (the first occurrence in the child-edge list)
    /// and its matching back-link. Consistent under parallel edges because AddChild appends to
    /// both sides in the same order. Returns false when no edge exists.
    /// </summary>
    public bool RemoveChild(DagNode<TValue, TEdge> child)
    {
      if (child == null)
        throw new ArgumentNullException(nameof(child));

      var childEdgeIndex = _ChildEdges.FindIndex(edge => ReferenceEquals(edge.Child, child));

      if (childEdgeIndex < 0)
        return false;

      _ChildEdges.RemoveAt(childEdgeIndex);

      var parentEdgeIndex = child._ParentEdges.FindIndex(parentEdge => ReferenceEquals(parentEdge.Parent, this));
      child._ParentEdges.RemoveAt(parentEdgeIndex);
      return true;
    }

    /// <summary>
    /// Stably sorts this node's out-edges in place, ascending by a key of the child NODE. The
    /// edge payloads travel with their edges (that is the point of edges being first-class), only
    /// the order under this parent changes -- a shared child's position under its other parents
    /// is untouched, and no back-links move.
    /// </summary>
    public void SortChildrenBy<TKey>(Func<DagNode<TValue, TEdge>, TKey> keySelector)
    {
      if (keySelector == null)
        throw new ArgumentNullException(nameof(keySelector));

      SortChildEdgesBy(edge => keySelector(edge.Child));
    }

    /// <summary>
    /// As <see cref="SortChildrenBy{TKey}"/> but keyed on the whole edge, payload included --
    /// e.g. sort children by descending ownership fraction.
    /// </summary>
    public void SortChildEdgesBy<TKey>(Func<DagEdge<TValue, TEdge>, TKey> keySelector)
    {
      if (keySelector == null)
        throw new ArgumentNullException(nameof(keySelector));

      // OrderBy rather than List.Sort for stability: edges that compare equal keep their
      // existing relative order, so sorting is a refinement of the consumer's insertion order.
      var sortedEdges = _ChildEdges.OrderBy(keySelector).ToList();

      _ChildEdges.Clear();
      _ChildEdges.AddRange(sortedEdges);
    }

    /// <summary>Stably sorts this node's out-edges in place with an explicit child-node comparer.</summary>
    public void SortChildren(IComparer<DagNode<TValue, TEdge>> comparer)
    {
      if (comparer == null)
        throw new ArgumentNullException(nameof(comparer));

      var sortedEdges = _ChildEdges.OrderBy(edge => edge.Child, comparer).ToList();

      _ChildEdges.Clear();
      _ChildEdges.AddRange(sortedEdges);
    }

    public override string ToString() => Value?.ToString() ?? "(null)";
  }

  /// <summary>A live, index-preserving read-only projection over a backing list.</summary>
  internal sealed class ProjectedListView<TSource, TItem> : IReadOnlyList<TItem>
  {
    public ProjectedListView(List<TSource> source, Func<TSource, TItem> projection)
    {
      _Source = source;
      _Projection = projection;
    }

    private readonly List<TSource> _Source;
    private readonly Func<TSource, TItem> _Projection;

    public TItem this[int index] => _Projection(_Source[index]);
    public int Count => _Source.Count;

    public IEnumerator<TItem> GetEnumerator()
    {
      foreach (var sourceItem in _Source)
        yield return _Projection(sourceItem);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}
