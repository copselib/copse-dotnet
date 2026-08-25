using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  // Hand-written twin of Copse.TreenumeratorWrapper (see TreenumeratorBase for the pair rationale).
  /// <summary>
  /// A base class for treenumerators that transform one inner traversal: holds the inner
  /// cursor as <see cref="InnerTreenumerator"/> and disposes it on teardown.
  /// </summary>
  public abstract class AsyncTreenumeratorWrapper<TInner, TNode>
    : AsyncTreenumeratorBase<TNode>
  {
    public AsyncTreenumeratorWrapper(
      Func<IAsyncTreenumerator<TInner>> innerTreenumeratorFactory)
    {
      InnerTreenumerator = innerTreenumeratorFactory();
    }

    protected IAsyncTreenumerator<TInner> InnerTreenumerator { get; }

    protected override async ValueTask OnDisposingAsync()
    {
      await base.OnDisposingAsync().ConfigureAwait(false);

      if (InnerTreenumerator != null)
        await InnerTreenumerator.DisposeAsync().ConfigureAwait(false);
    }
  }

  /// <summary>The value-preserving form of
  /// <see cref="AsyncTreenumeratorWrapper{TInner, TNode}"/>: inner and outer share one node
  /// type.</summary>
  public abstract class AsyncTreenumeratorWrapper<TNode> : AsyncTreenumeratorWrapper<TNode, TNode>
  {
    protected AsyncTreenumeratorWrapper(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory)
      : base(innerTreenumeratorFactory)
    {
    }
  }
}
