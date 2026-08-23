namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>The upward flow door: <c>dag.Sinkfix().Scan&lt;TResult&gt;(…)</c> / <c>.Dispatch&lt;TDispatch&gt;(…)</c> / <c>.DispatchEdges&lt;TDispatch&gt;(…)</c> with one type argument (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>
    public static DagFlow<TNode, TEdge> Sinkfix<TNode, TEdge>(this IDagnumerable<TNode, TEdge> source)
      => new DagFlow<TNode, TEdge>(source, DagFlowOrientation.Sinkfix);
  }
}
