namespace Copse.Linq.Async.Treenumerables
{
  // The projection wrapper's half of the compose-left door -- a PARTIAL part, deliberately
  // outside the CompositeToNarrow fan-out (the narrow twins do not claim the door: their
  // inner sources are single-dimension, and the consumer contract is composite-width).
  // The wrapper's whole contribution is the generic instantiation: this is the one scope
  // where TSource -- the consumer's existential TInner -- can be spelled.
  //
  // Explicit implementation, deliberately: the compose-left door is PARKED as internal
  // (SELECT_INTO_CAPTURES_DESIGN.md -- third parties get compose-right only), and the
  // class going public must not unpark it.
  partial class AsyncSelectTreenumerable<TSource, TResult> : IAsyncProjectionSource<TResult>
  {
    TOperatorResult IAsyncProjectionSource<TResult>.CaptureThrough<TOperatorResult>(IAsyncProjectionConsumer<TResult, TOperatorResult> consumer)
      => consumer.Consume(_Source, _Selector);
  }
}
