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

    /// <summary>
    /// The orientation flip over the contract: <c>Materialize().Transpose()</c>, spelled once.
    /// MATERIALIZES by definition when the source is not already a buffer -- the return type
    /// declares it (the laziness policy's documented-when-not clause): a transpose is a
    /// whole-graph fact, so a lazy transposed stream cannot honestly exist.
    /// </summary>
    public static DagBuffer<TNode, TEdge> Transpose<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
      => DagBuffer<TNode, TEdge>.From(source).Transpose();
  }
}
