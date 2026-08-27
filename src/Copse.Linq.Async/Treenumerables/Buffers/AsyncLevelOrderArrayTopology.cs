using Copse.Core;
using Copse;
using Copse.Stores;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerables
{
  // The COMPLETED level-order citizen (the adjacency split): the layout's native
  // affordances answer children (a bounds probe plus an offset -- contiguous runs) and roots
  // (the leading entries) with no state at all, exactly as the growing engine already does.
  // Only the parent axis needs an index, and on a finished store the incremental
  // suspendable sweep collapses to one two-cursor merge -- child runs tile the buffer in
  // parent order, so a single pass writes the exact int[] parent map on the first parent
  // probe. Every probe after is an array read. Contrast AsyncLevelOrderAdjacencyIndex, the
  // GROWING citizen, which keeps the sweep suspendable because its store may still be fed.
  // Single-threaded by contract, like every adjacency engine.
  internal sealed class AsyncLevelOrderArrayTopology<TNode> : IAsyncTreeTopology<TNode, int>
  {
    public AsyncLevelOrderArrayTopology(AsyncLevelOrderArrayStore<TNode> store)
    {
      _Store = store;
    }

    private const int NoParent = -1;

    private readonly AsyncLevelOrderArrayStore<TNode> _Store;

    internal AsyncLevelOrderArrayStore<TNode> Store => _Store;

    // Built on the first parent probe; exact size, no per-node machinery.
    private int[] _ParentIndexes;

    public ValueTask<TNode> GetNodeAsync(int handle)
      => new ValueTask<TNode>(_Store.GetNode(handle));

    public async ValueTask<Option<int>> TryGetParentAsync(int handle)
    {
      if (_ParentIndexes == null)
        _ParentIndexes = await BuildParentIndexesAsync().ConfigureAwait(false);

      var parentIndex = _ParentIndexes[handle];

      return parentIndex == NoParent
        ? default
        : new Option<int>(parentIndex);
    }

    public async ValueTask<Option<HandleAndSiblingIndex<int>>> TryGetChildAtAsync(int handle, int childIndex)
    {
      if (childIndex < 0 || !await _Store.EnsureChildAvailableAsync(handle, childIndex).ConfigureAwait(false))
        return default;

      // GetFirstChildIndex is meaningful once the parent has an available child, which the
      // successful probe above just established.
      return new Option<HandleAndSiblingIndex<int>>(new HandleAndSiblingIndex<int>(_Store.GetFirstChildIndex(handle) + childIndex, childIndex));
    }

    public async ValueTask<Option<HandleAndSiblingIndex<int>>> TryGetRootAtAsync(int rootIndex)
    {
      if (rootIndex < 0 || !await _Store.EnsureRootAvailableAsync(rootIndex).ConfigureAwait(false))
        return default;

      // Root ordinal k is buffer index k: the roots are the store's leading entries.
      return new Option<HandleAndSiblingIndex<int>>(new HandleAndSiblingIndex<int>(rootIndex, rootIndex));
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
