namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The explicit escalation to the capture tier: consumes the live stream ONCE (one pass,
    /// dispatch contiguity filling the CSR arrays in stream order) and returns the owned,
    /// re-traversable <see cref="DagBuffer{TNode, TEdge}"/>. A buffer materializes to itself.
    /// </summary>
    public static DagBuffer<TNode, TEdge> Materialize<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
      => DagBuffer<TNode, TEdge>.From(source);
  }
}
