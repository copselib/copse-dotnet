namespace Copse.Linq.Async.Treenumerables
{
  // The projection wrapper's half of the compose-left door -- a PARTIAL part, deliberately
  // outside the CompositeToNarrow fan-out (the narrow twins do not claim the door: their
  // inner sources are single-dimension, and the consumer contract is composite-width).
  // The wrapper's whole contribution is the generic instantiation: this is the one scope
  // where TSource -- the consumer's existential TInner -- can be spelled.
  internal sealed partial class AsyncSelectTreenumerable<TSource, TResult> : IAsyncProjectionSource<TResult>
  {
    public TOperatorResult CaptureThrough<TOperatorResult>(IAsyncProjectionConsumer<TResult, TOperatorResult> consumer)
      => consumer.Consume(_Source, _Selector);
  }
}
