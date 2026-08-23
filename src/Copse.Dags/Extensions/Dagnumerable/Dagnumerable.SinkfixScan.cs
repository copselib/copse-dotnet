using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Upward cumulative scan over the contract -- SOURCEFIX-OF-THE-TRANSPOSE, served by the
    /// same fold core read upward (the derivation ruling; the transpose law is
    /// pinned by the coherence battery): <paramref name="accumulate"/> receives each node and
    /// one EDGE-PAIRED result per live out-edge -- the child's accumulated result with the
    /// payload of the edge to it, in OUT-EDGE order; empty at sinks, which is the call that
    /// seeds the scan. Each node is computed exactly once no matter how many parents share it;
    /// a shared child's (single, reused) result appears in EACH parent's list, and parallel
    /// edges contribute it twice -- the diamond question stays the caller's explicit choice
    /// (combine per-edge results for per-use roll-ups; use
    /// <see cref="SinkfixDispatch{TNode, TDispatch, TEdge}"/> for attribution that must not
    /// double-count). Materializes by theorem (a sinkfix result is children-first: the whole
    /// graph precedes the first result) and returns the CANONICAL PAIRING over the source's
    /// shared structure, in the source's own orientation.
    /// </summary>
    internal static DagBuffer<DagScanResult<TNode, TResult>, TEdge> SinkfixScan<TNode, TResult, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
    {
      if (accumulate == null)
        throw new ArgumentNullException(nameof(accumulate));

      return ScanBuffer(source.Materialize(), DagFlowOrientation.Sinkfix, accumulate);
    }
  }
}
