namespace Copse.Linq.Async.Treenumerables
{
  // The collapsed chain's half of the compose-left door -- a PARTIAL part, deliberately
  // outside the CompositeToNarrow fan-out (the consumer contract is composite-width; narrow
  // parity is deferred with the rest of the doors). This is the one scope where TSource and
  // TResultSelector -- the consumer's existential TInner and TArrow -- can be spelled.
  //
  // Explicit implementation, deliberately: the door is internal, and the class going public
  // must not unpark it.
  partial class AsyncSelectWhereTreenumerable<TSource, TResult, TResultSelector> : IAsyncResultSource<TResult>
  {
    TOperatorResult IAsyncResultSource<TResult>.CaptureThrough<TOperatorResult>(IAsyncResultConsumer<TResult, TOperatorResult> consumer)
      => consumer.Consume<TSource, TResultSelector>(_Source, _ResultSelector);
  }
}
