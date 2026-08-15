using Copse.Async;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Topologies
{
  // The SPI reconstituted from a vantage (2026-08-15, the seed-what-breaks re-plumb): every
  // answer is a public walker step, so this adapter -- and everything built over it -- uses
  // no access a third party lacks. It exists because the machinery's native currency is the
  // topology (the Walk adapter and the lens delegations all consume the SPI), while what a
  // walker publicly affords is steps; this is the bridge, replacing the family-IVT
  // extraction of the walker's private topology. One extra struct copy per answer (the At),
  // then the same single probe the extraction paid.
  internal sealed class AsyncWalkerTopology<TValue, THandle> : IAsyncTreeTopology<TValue, THandle>
  {
    public AsyncWalkerTopology(AsyncTreeWalker<TValue, THandle> walker)
    {
      _Walker = walker;
    }

    private readonly AsyncTreeWalker<TValue, THandle> _Walker;

    public ValueTask<TValue> GetValueAsync(THandle handle) => _Walker.At(handle).GetValueAsync();

    public async ValueTask<ParentResult<THandle>> TryGetParentAsync(THandle handle)
    {
      var step = await _Walker.At(handle).MoveToParentAsync().ConfigureAwait(false);

      return step.HasWalker ? new ParentResult<THandle>(step.Walker.Focus) : default;
    }

    public async ValueTask<ChildResult<THandle>> TryGetChildAtAsync(THandle handle, int childIndex)
    {
      var step = await _Walker.At(handle).MoveToChildAsync(childIndex).ConfigureAwait(false);

      return step.HasWalker
        ? new ChildResult<THandle>(new NodeAndSiblingIndex<THandle>(step.Walker.Focus, childIndex))
        : default;
    }

    public async ValueTask<ChildResult<THandle>> TryGetRootAtAsync(int rootIndex)
    {
      var step = await _Walker.MoveToRootAsync(rootIndex).ConfigureAwait(false);

      return step.HasWalker
        ? new ChildResult<THandle>(new NodeAndSiblingIndex<THandle>(step.Walker.Focus, rootIndex))
        : default;
    }
  }
}
