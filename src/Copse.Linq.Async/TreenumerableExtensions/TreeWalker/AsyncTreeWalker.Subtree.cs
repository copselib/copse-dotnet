using Copse.Core;
using Copse;
using Copse.Linq.Treenumerables;

namespace Copse.Linq
{
  public static partial class AsyncTreeWalker
  {
    /// <summary>The reverse door -- the inclusive hoist: the treenumerable this stance
    /// denotes. At a node, the subtree rooted at the focus, as a severed re-rooted view
    /// sharing the source's handles -- identical to the label <c>Subtrees()</c> stamps at
    /// this focus (pinned). At the UNFOCUSED STANCE, the source forest itself: there is nothing
    /// above it to sever, and it contributes no row of its own -- it has no
    /// value, and a valueless node has no spelling in the treenumerable, so the focus drops
    /// out by type, never by rule. The round trip source → door → <c>Subtree()</c> recovers
    /// the source (the identity, with no case analysis); the interior round trip lands at
    /// the same focus but FORGETS the upward context (severance is the cofree forgetting --
    /// deliberate, and the reason the two round trips are not symmetric).</summary>
    public static IAsyncWalkableTreenumerable<TNode, THandle> Subtree<TNode, THandle>(
      this AsyncTreeWalker<TNode, THandle> walker)
    {
      if (!walker.HasFocus)
        return new AsyncTopologyWalkable<TNode, THandle>(walker.Topology);

      return new AsyncSubtreeWalkable<TNode, THandle>(walker.Topology, walker.Focus);
    }
  }
}
