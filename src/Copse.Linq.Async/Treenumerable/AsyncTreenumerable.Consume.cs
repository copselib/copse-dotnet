using Copse.Core;
using Copse.Core.Async;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: drives the tree to exhaustion (depth-first) for its side effects, discarding
    /// the visit stream. MECHANICAL, unconditionally -- this is the hammer unit tests and
    /// benchmarks rely on: a buffer is walked like anything else (its inert replay touches no
    /// source), a deferred capture is forced by the walk, and a lazy buffer's capture completes
    /// as a side effect of being walked. For settling a capture with the MINIMUM work instead,
    /// use the lazy buffer's <c>Complete()</c> or <c>Materialize</c>. Awaitable -&gt; carries
    /// the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncTreenumerator(TreeTraversalStrategy.DepthFirst);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>As <c>ConsumeAsync(source)</c>, walking the named dimension.</summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>The depth-first-narrow entry: the same unconditional walk.</summary>
    public static async ValueTask ConsumeAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>The breadth-first-narrow twin.</summary>
    public static async ValueTask ConsumeAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }
  }
}
