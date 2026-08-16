using Copse.Async;
using Copse.Async.Stores;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The COMPLETED level-order citizen (the 2026-08-16 adjacency split): the layout's native
  // affordances answer children (a bounds probe plus an offset -- contiguous runs) and roots
  // (the leading entries) with no state at all, exactly as the growing engine already does.
  // Only the parent axis needs an index, and on a finished store the incremental
  // suspendable sweep collapses to one two-cursor merge -- child runs tile the buffer in
  // parent order, so a single pass writes the exact int[] parent map on the first parent
  // probe. Every probe after is an array read. Contrast AsyncLevelOrderAdjacencyIndex, the
  // GROWING citizen, which keeps the sweep suspendable because its store may still be fed.
  // Single-threaded by contract, like every adjacency engine.
  internal sealed class AsyncLevelOrderArrayTopology<TValue> : IAsyncTreeTopology<TValue, int>
  {
    public AsyncLevelOrderArrayTopology(AsyncLevelOrderArrayStore<TValue> store)
    {
      _Store = store;
    }

    private const int NoParent = -1;

    private readonly AsyncLevelOrderArrayStore<TValue> _Store;

    internal AsyncLevelOrderArrayStore<TValue> Store => _Store;

    // Built on the first parent probe; exact size, no per-node machinery.
    private int[] _ParentIndexes;

    public ValueTask<TValue> GetValueAsync(int handle)
      => new ValueTask<TValue>(_Store.GetValue(handle));

    public async ValueTask<ParentResult<int>> TryGetParentAsync(int handle)
    {
      if (_ParentIndexes == null)
        _ParentIndexes = await BuildParentIndexesAsync().ConfigureAwait(false);

      var parentIndex = _ParentIndexes[handle];

      return parentIndex == NoParent
        ? default
        : new ParentResult<int>(parentIndex);
    }

    public async ValueTask<ChildResult<int>> TryGetChildAtAsync(int handle, int childIndex)
    {
      if (childIndex < 0 || !await _Store.EnsureChildAvailableAsync(handle, childIndex).ConfigureAwait(false))
        return default;

      // GetFirstChildIndex is meaningful once the parent has an available child, which the
      // successful probe above just established.
      return new ChildResult<int>(new NodeAndSiblingIndex<int>(_Store.GetFirstChildIndex(handle) + childIndex, childIndex));
    }

    public async ValueTask<ChildResult<int>> TryGetRootAtAsync(int rootIndex)
    {
      if (rootIndex < 0 || !await _Store.EnsureRootAvailableAsync(rootIndex).ConfigureAwait(false))
        return default;

      // Root ordinal k is buffer index k: the roots are the store's leading entries.
      return new ChildResult<int>(new NodeAndSiblingIndex<int>(rootIndex, rootIndex));
    }

    // The stackless two-cursor merge, run to completion: seed the roots (the leading
    // entries), then advance the parent cursor through the buffer writing itself under each
    // of its children. Completeness means every availability probe answers immediately and
    // the cursor never suspends. The store's ValueTasks complete synchronously; the awaits
    // are contract courtesy, not suspension points.
    private async ValueTask<int[]> BuildParentIndexesAsync()
    {
      var parentIndexes = new int[_Store.Count];
      var writtenCount = 0;

      while (await _Store.EnsureRootAvailableAsync(writtenCount).ConfigureAwait(false))
      {
        parentIndexes[writtenCount] = NoParent;
        writtenCount++;
      }

      var parentCursor = 0;

      while (writtenCount < parentIndexes.Length)
      {
        var childOrdinal = 0;

        while (await _Store.EnsureChildAvailableAsync(parentCursor, childOrdinal).ConfigureAwait(false))
        {
          parentIndexes[_Store.GetFirstChildIndex(parentCursor) + childOrdinal] = parentCursor;
          childOrdinal++;
          writtenCount++;
        }

        parentCursor++;
      }

      return parentIndexes;
    }
  }
}
