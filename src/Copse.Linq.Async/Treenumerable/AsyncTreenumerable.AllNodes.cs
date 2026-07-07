using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: whether every node satisfies the predicate -- the negation of "any node fails it".
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<bool> AllNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => !await source.AnyNodesAsync(nodeContext => !predicate(nodeContext)).ConfigureAwait(false);
  }
}
