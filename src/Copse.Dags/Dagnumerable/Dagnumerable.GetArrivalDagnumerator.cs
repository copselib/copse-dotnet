using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Acquires the ARRIVAL-PROTOCOL presentation of a forward source
    /// (design-docs/DAG_CONTRACT_DESIGN.md, the arrival protocol; phase 1, vocabulary provisional):
    /// one <see cref="DagNodeEvent{TNode, TEdge}"/> per live node in topological order --
    /// arrival group, node, departure group -- with per-edge sever/suppress verdicts
    /// answering each event. Synthesized as a grouping layer over the visit protocol
    /// (dispatch contiguity supplies the departure groups; topological order completes the
    /// arrival groups before each event; O(frontier) buffering), so it composes over any
    /// forward source, wrapped operators included, ordinals preserved.
    /// </summary>
    public static IArrivalDagnumerator<TNode, TEdge> GetArrivalDagnumerator<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      return new ArrivalDagnumerator<TNode, TEdge>(source.GetDagnumerator());
    }
  }
}
