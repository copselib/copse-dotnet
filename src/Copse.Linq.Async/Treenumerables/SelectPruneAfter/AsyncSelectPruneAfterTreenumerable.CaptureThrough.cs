namespace Copse.Linq.Async.Treenumerables
{
  // The middle tier's half of the compose-left door -- a PARTIAL part outside the
  // CompositeToNarrow fan-out. Its in-tier arrow is delegate-bound by nature (only
  // composition builds this wrapper), so it surrenders as the one FuncResultSelector leaf
  // it already rides when a rejecting operator splices over it.
  partial class AsyncSelectPruneAfterTreenumerable<TSource, TResult> : IAsyncResultSource<TResult>
  {
    TOperatorResult IAsyncResultSource<TResult>.CaptureThrough<TOperatorResult>(IAsyncResultConsumer<TResult, TOperatorResult> consumer)
      => consumer.Consume<TSource, FuncResultSelector<TSource, TResult>>(_Source, new FuncResultSelector<TSource, TResult>(_ResultSelector));
  }
}
