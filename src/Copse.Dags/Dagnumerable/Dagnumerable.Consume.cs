namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Drains the walk completely and keeps nothing -- the tree family's Consume, dag-side,
    /// and the family's drain-without-residency VALIDATOR (THE LAZY BUILDER RULING,
    ///, design-docs/DAG_CONTRACT_DESIGN.md): a full drain of a cyclic source throws
    /// <see cref="DagCycleException"/> at the starvation point, so completing is the proof --
    /// O(queue) memory, no capture. Three postures, zero new vocabulary: any full drain
    /// validates, <c>Materialize</c> validates and keeps the certificate, <c>Consume</c>
    /// validates and discards. Note what discarding costs: the proof is about THIS drain --
    /// the builder is mutable, so only the buffer certifies a value that cannot drift. Also
    /// the effect-chain terminal, as tree-side: Do-effects fire per drain, and Consume is the
    /// drain that wants nothing back.
    /// </summary>
    public static void Consume<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
    {
      using var walk = source.GetDagnumerator();

      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
      }
    }
  }
}
