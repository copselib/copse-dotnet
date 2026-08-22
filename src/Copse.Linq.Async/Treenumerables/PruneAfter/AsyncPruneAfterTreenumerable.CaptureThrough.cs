namespace Copse.Linq.Async.Treenumerables
{
  // The light wrapper's half of the compose-left door -- a PARTIAL part outside the
  // CompositeToNarrow fan-out, like the collapsed chain's. The lattice keeps prune-after as
  // a layer of its own (joining would demote its representation), but a consumer that ends
  // the chain -- bind, folding the arrow ahead of its selector -- has no representation to
  // demote, so the wrapper surrenders its source and its predicate as the arrow it already
  // is. The consumer recurses into the source if that can surrender too.
  partial class AsyncPruneAfterTreenumerable<TNode> : IAsyncResultSource<TNode>
  {
    TOperatorResult IAsyncResultSource<TNode>.CaptureThrough<TOperatorResult>(IAsyncResultConsumer<TNode, TOperatorResult> consumer)
      => consumer.Consume<TNode, PruneAfterResultSelector<TNode>>(_Source, new PruneAfterResultSelector<TNode>(_Predicate));
  }
}
