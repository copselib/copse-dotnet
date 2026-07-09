using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  // Hand-written twin of Copse.TreenumeratorWrapper (see TreenumeratorBase for the pair rationale):
  // an async treenumerator that wraps a single inner async cursor, disposing it on teardown.
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

  public abstract class AsyncTreenumeratorWrapper<TNode> : AsyncTreenumeratorWrapper<TNode, TNode>
  {
    protected AsyncTreenumeratorWrapper(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory)
      : base(innerTreenumeratorFactory)
    {
    }
  }
}
