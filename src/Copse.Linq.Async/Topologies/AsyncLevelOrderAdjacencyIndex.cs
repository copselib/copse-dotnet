using Copse.Collections;
using Copse.Core;
using Copse;
using Copse.Stores;
using System.Threading.Tasks;

namespace Copse.Linq.Topologies
{
  // The GROWING level-order citizen (completed stores ride AsyncLevelOrderArrayTopology,
  // the adjacency split): the layout's native affordances need almost no index --
  // children are a bounds probe plus an offset (contiguous runs), roots are the leading
  // entries -- so only the parent axis carries state: the walker PoC's stackless two-cursor
  // merge (child runs tile the buffer in parent order), restructured as an INCREMENTAL sweep
  // suspendable mid-child-group. Each resume demands exactly the next child-availability
  // answer from the store (grow-precedes-read), so probes force a growing feed only as far as
  // their answer needs -- and on level-order that is one level ahead at most, the layout's
  // cheap direction. The parent cursor can never overtake the growth it causes: it advances
  // past a parent only when that parent's child run has closed. Single-threaded by contract.
  internal sealed class AsyncLevelOrderAdjacencyIndex<TNode, TStore> : IAsyncTreeTopology<TNode, int>
    where TStore : IAsyncLevelOrderStore<TNode>
  {
    public AsyncLevelOrderAdjacencyIndex(TStore store)
    {
      _Store = store;
    }

    private const int NoParent = -1;

    private readonly TStore _Store;

    // The reclaim seam: the store, and whether any probe has advanced the
    // index -- re-seating over the built array store is free only while untouched.
    internal TStore Store => _Store;

    internal bool ScanUntouched => !_RootsSeeded && _ParentIndexes.Count == 0;

    private readonly RefAppendOnlyList<int> _ParentIndexes = new RefAppendOnlyList<int>();
    private bool _RootsSeeded;
    private int _ParentCursor;
    private int _ChildOrdinal;
    private int _FirstChildIndex;

    public async ValueTask<TNode> GetNodeAsync(int handle)
    {
      await ExtendParentIndexesAsync(handle).ConfigureAwait(false);
      return _Store.GetNode(handle);
    }

    public async ValueTask<Option<int>> TryGetParentAsync(int handle)
    {
      if (!await ExtendParentIndexesAsync(handle).ConfigureAwait(false))
        return default;

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

    // Sweep the parent index forward until it covers targetIndex (false iff the tree ends
    // first). The roots seed the index -- level order delivers the whole root group before any
    // child, so seeding is level-0-bounded -- then the parent cursor writes itself under each
    // of its children, suspendable between any two probes.
    private async ValueTask<bool> ExtendParentIndexesAsync(int targetIndex)
    {
      if (!_RootsSeeded)
      {
        var rootOrdinal = 0;

        while (await _Store.EnsureRootAvailableAsync(rootOrdinal).ConfigureAwait(false))
        {
          _ParentIndexes.AddLast(NoParent);
          rootOrdinal++;
        }

        _RootsSeeded = true;
      }

      while (_ParentIndexes.Count <= targetIndex)
      {
        if (_ParentCursor >= _ParentIndexes.Count)
          return false;

        if (await _Store.EnsureChildAvailableAsync(_ParentCursor, _ChildOrdinal).ConfigureAwait(false))
        {
          if (_ChildOrdinal == 0)
            _FirstChildIndex = _Store.GetFirstChildIndex(_ParentCursor);

          var childIndex = _FirstChildIndex + _ChildOrdinal;

          while (_ParentIndexes.Count <= childIndex)
            _ParentIndexes.AddLast(NoParent);

          _ParentIndexes[childIndex] = _ParentCursor;
          _ChildOrdinal++;
        }
        else
        {
          _ParentCursor++;
          _ChildOrdinal = 0;
        }
      }

      return true;
    }
  }
}
