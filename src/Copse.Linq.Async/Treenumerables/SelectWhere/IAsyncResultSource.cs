using Copse.Core.Async;

namespace Copse.Linq.Async.Treenumerables
{
  /// <summary>
  /// The compose-left door for the collapsed chain: the rejecting twin of
  /// <see cref="IAsyncProjectionSource{TProjected}"/>. A chain wrapper surrenders its
  /// un-rejected inner source and its struct arrow to a consumer that can build its own
  /// machinery over any inner type, given the arrow into its domain -- bind folds the
  /// arrow ahead of its selector (SELECTMANY_DESIGN.md Addendum V). Internal, like the
  /// projection door: third parties get compose-right only.
  /// </summary>
  internal interface IAsyncResultSource<TResult>
  {
    TOperatorResult CaptureThrough<TOperatorResult>(IAsyncResultConsumer<TResult, TOperatorResult> consumer);
  }

  /// <summary>The consumer half: instantiated with the wrapper's hidden inner type and arrow type,
  /// so the arrow stays a struct leg in the consumer's machinery.</summary>
  internal interface IAsyncResultConsumer<TResult, TOperatorResult>
  {
    TOperatorResult Consume<TInner, TArrow>(IAsyncTreenumerable<TInner> innerSource, TArrow arrow)
      where TArrow : struct, IResultSelector<TInner, TResult>;
  }
}
