using Copse.Async;
using Copse.Linq.Async.Treenumerables;

namespace Copse.Linq
{
  public static partial class AsyncTreeWalker
  {
    /// <summary>The reverse door: the treenumerable this stance denotes -- the subtree
    /// rooted at the focus, as a severed re-rooted view sharing the source's handles.
    /// Identical to the label <c>Subtrees()</c> stamps at this focus (pinned). The round
    /// trip tree → root walker → <c>Subtree()</c> recovers the tree (the counit in
    /// interchange clothing); the other round trip lands at the same focus but FORGETS the
    /// upward context (severance is the cofree forgetting -- deliberate, and the reason the
    /// two round trips are not symmetric).</summary>
    public static IAsyncWalkableTreenumerable<TValue, THandle> Subtree<TValue, THandle>(
      this AsyncTreeWalker<TValue, THandle> walker)
      => new AsyncSubtreeWalkable<TValue, THandle>(walker.Topology, walker.Focus);
  }
}
