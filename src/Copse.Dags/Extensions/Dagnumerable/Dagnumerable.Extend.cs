using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The comonad's co-bind: relabel every node by an arbitrary OBSERVATION of its focus --
    /// the observer receives the source topology and the handle, a whole vantage (upstream
    /// arrivals, the downstream cone, degrees), where a streaming Select sees one value.
    /// Shape and handles untouched; lazy (a lens view) over the door knocked once. Every
    /// Sourcefix/Sinkfix fold is an extend with a schedule the observer's shape admits --
    /// the coherence pins say which: the scan tier keeps O(V+E) for semiring-shaped
    /// observers; a general closure observer is path-priced (the path-semantics canary).
    /// </summary>
    public static IWalkableDagnumerable<TResult, THandle, TEdge> Extend<TValue, THandle, TEdge, TResult>(
      this IWalkableDagnumerable<TValue, THandle, TEdge> source,
      Func<IDagTopology<TValue, THandle, TEdge>, THandle, TResult> observer)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (observer == null)
        throw new ArgumentNullException(nameof(observer));

      return new DagExtendWalkable<TValue, THandle, TEdge, TResult>(DagTopology.Lazy(source), observer);
    }

    // The topology-receiver form: the algebra at SPI altitude, for machinery holding a topology.
    internal static IWalkableDagnumerable<TResult, THandle, TEdge> Extend<TValue, THandle, TEdge, TResult>(
      this IDagTopology<TValue, THandle, TEdge> source,
      Func<IDagTopology<TValue, THandle, TEdge>, THandle, TResult> observer)
      => new DagExtendWalkable<TValue, THandle, TEdge, TResult>(source, observer);
  }
}
