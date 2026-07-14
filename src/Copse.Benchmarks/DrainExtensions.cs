using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using System.Threading.Tasks;

namespace Copse.Benchmarks
{
  // The benchmark-side drain: walk the visit stream to exhaustion through a real treenumerator.
  // Exists because Treenumerable.Consume PROBES (2026-07-14): on buffers and memos it settles
  // the capture -- a no-op once complete -- instead of replaying. Correct for the product, but
  // rows that measure REPLAY traversal over a capture must drive the treenumerator explicitly,
  // or they silently measure the no-op.
  internal static class DrainExtensions
  {
    public static void Drain<TNode>(this ITreenumerable<TNode> source, TreeTraversalStrategy treeTraversalStrategy)
    {
      var treenumerator = source.GetTreenumerator(treeTraversalStrategy);
      using (treenumerator)
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
        }
    }

    public static async ValueTask DrainAsync<TNode>(this IAsyncTreenumerable<TNode> source, TreeTraversalStrategy treeTraversalStrategy)
    {
      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
        }
    }
  }
}
