namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>The downward flow: <c>dag.Sourcefix().Scan  /// <summary>The downward flow door: <c>dag.Sourcefix().Scan&lt;TResult&gt;(…)</c> / <c>.Dispatch&lt;TDispatch&gt;(…)</c> / <c>.DispatchEdges&lt;TDispatch&gt;(…)</c> with one type argument (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>lt;TResult  /// <summary>The downward flow door: <c>dag.Sourcefix().Scan&lt;TResult&gt;(…)</c> / <c>.Dispatch&lt;TDispatch&gt;(…)</c> / <c>.DispatchEdges&lt;TDispatch&gt;(…)</c> with one type argument (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>gt;(…)</c> / <c>.Dispatch(seed, …)</c> / <c>.DispatchEdges  /// <summary>The downward flow door: <c>dag.Sourcefix().Scan&lt;TResult&gt;(…)</c> / <c>.Dispatch&lt;TDispatch&gt;(…)</c> / <c>.DispatchEdges&lt;TDispatch&gt;(…)</c> with one type argument (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>lt;TDispatch  /// <summary>The downward flow door: <c>dag.Sourcefix().Scan&lt;TResult&gt;(…)</c> / <c>.Dispatch&lt;TDispatch&gt;(…)</c> / <c>.DispatchEdges&lt;TDispatch&gt;(…)</c> with one type argument (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>gt;(…)</c> -- THE spelling of the sourcefix family (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>
    public static DagFlow<TNode, TEdge> Sourcefix<TNode, TEdge>(this IDagnumerable<TNode, TEdge> source)
      => new DagFlow<TNode, TEdge>(source, DagFlowOrientation.Sourcefix);
  }
}
