using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The light tier's narrow (depth-first-only) half: never-SkipNode chains over a
  // single-dimension source. The same two type-enforced doors as
  // IAsyncSelectPruneAfterTreenumerable -- a projection returns a bare value and a prune-after
  // returns a bool, so neither can smuggle SkipNode -- and the tier never moves a label, so
  // implementers' Relabels is always false and even positional lambdas compose across them.
  internal interface IAsyncSelectPruneAfterDepthFirstTreenumerable<TNode> : IAsyncSelectWhereDepthFirstTreenumerable<TNode>
  {
    // Compose a projection, staying on the tier's light machinery.
    IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector);

    // Compose a prune-after (keep the node, shed its subtree on a match), staying on the
    // tier's light machinery.
    IAsyncDepthFirstTreenumerable<TNode> ComposePruneAfter(Func<NodeContext<TNode>, bool> predicate);
  }
}
