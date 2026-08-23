namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>The downward flow: <c>dag.Sourcefix().Scan&lt;TResult&gt;(…)</c>, <c>.Dispatch(seed, …)</c>, <c>.DispatchEdges&lt;TDispatch&gt;(…)</c> (see <see cref="DagFlow{TNode, TEdge}"/>).</summary>
    public static DagFlow<TNode, TEdge> Sourcefix<TNode, TEdge>(this IDagnumerable<TNode, TEdge> source)
      => new DagFlow<TNode, TEdge>(source, DagFlowOrientation.Sourcefix);
  }
}
