using Copse.Collections;
using Copse.Core;
using Copse;
using Copse.Stores;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerables
{
  // The GROWING preorder citizen (completed stores ride AsyncPreorderArrayTopology, the
  // adjacency split): the walker PoC's one-pass index build (open-span stack ->
  // parents; discovery order -> child links; stack-empty points -> roots), restructured as
  // an INCREMENTAL SCAN for stores still being fed. The scan cursor advances one node at a
  // time, each advance demanding exactly one more buffered node (grow-precedes-read), and
  // every probe extends the scan only as far as its answer needs -- the span-bounded
  // promise of the walkable contract, by construction.
  //
  // The child axis is three parallel linked arrays (first child / next sibling / last
  // child), all RefAppendOnlyList<int>: one slot per scanned node, zero per-node objects
  // (a per-node child list would allocate one List per scanned node). An ordinal probe walks the sibling chain, so a one-slot cursor (parent, ordinal,
  // child) keeps sequential child iteration -- the walker's dominant pattern -- O(1)
  // amortized; chain links are written once and never move, so the cursor never goes stale.
  //
  // The pop rule tolerates growth: an open span's size reads 0 until the store grows past
  // its close, so an ancestor of the frontier simply stays on the stack ("topSize > 0"
  // gates the pop) and is re-checked when the scan resumes -- the parent recorded for each
  // node is the stack top at its scan moment, which is correct whether or not the enclosing
  // spans have closed yet. Single-threaded by contract, like the memo feeds it may drive.
  internal sealed class AsyncPreorderAdjacencyIndex<TNode, TStore> : IAsyncTreeTopology<TNode, int>
    where TStore : IAsyncPreorderStore<TNode>
  {
    public AsyncPreorderAdjacencyIndex(TStore store)
    {
      _Store = store;
    }

    private const int NoParent = -1;
    private const int NoIndex = -1;

    private readonly TStore _Store;

    // The bulk-fold seam: a completed store hands whole-tree algorithms its raw arithmetic
    // (Count/GetNode/GetSubtreeSize), bypassing per-probe dispatch -- the receiver-smart
    // fast path's door (the bulk-fold seam, absorbed into LeaffixScan).
    internal TStore Store => _Store;

    // The reclaim seam: true iff no probe has advanced the scan -- re-seating is free.
    internal bool ScanUntouched => _ScanCursor == 0;

    private readonly RefAppendOnlyList<int> _ParentIndexes = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _FirstChildIndexes = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _NextSiblingIndexes = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _LastChildIndexes = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _RootIndexes = new RefAppendOnlyList<int>();
    private readonly RefSemiDeque<int> _OpenSpanStack = new RefSemiDeque<int>();
    private int _ScanCursor;
    private bool _Exhausted;

    // The sequential-iteration cursor: the last answered (parent, ordinal, child) triple.
    private int _CursorParent = NoParent;
    private int _CursorOrdinal;
    private int _CursorChild;

    public async ValueTask<TNode> GetNodeAsync(int handle)
    {
      await _Store.EnsureBufferedAsync(handle).ConfigureAwait(false);
      return _Store.GetNode(handle);
    }

    public async ValueTask<Option<int>> TryGetParentAsync(int handle)
    {
      while (_ScanCursor <= handle)
      {
        if (!await ScanForwardAsync().ConfigureAwait(false))
          return default;
      }

      var parentIndex = _ParentIndexes[handle];

      return parentIndex == NoParent
        ? default
        : new Option<int>(parentIndex);
    }

    public async ValueTask<Option<HandleAndSiblingIndex<int>>> TryGetChildAtAsync(int handle, int childIndex)
    {
      if (childIndex < 0)
        return default;

      while (_ScanCursor <= handle)
      {
        if (!await ScanForwardAsync().ConfigureAwait(false))
          return default;
      }

      int ordinal;
      int child;

      if (_CursorParent == handle && _CursorOrdinal <= childIndex)
      {
        ordinal = _CursorOrdinal;
        child = _CursorChild;
      }
      else
      {
        while (_FirstChildIndexes[handle] == NoIndex)
        {
          // No further children can appear once the handle's span has closed behind the scan.
          if (SpanClosedBehindScan(handle))
            return default;

          if (!await ScanForwardAsync().ConfigureAwait(false))
            return default;
        }

        ordinal = 0;
        child = _FirstChildIndexes[handle];
      }

      while (ordinal < childIndex)
      {
        while (_NextSiblingIndexes[child] == NoIndex)
        {
          if (SpanClosedBehindScan(handle))
            return default;

          if (!await ScanForwardAsync().ConfigureAwait(false))
            return default;
        }

        child = _NextSiblingIndexes[child];
        ordinal++;
      }

      _CursorParent = handle;
      _CursorOrdinal = childIndex;
      _CursorChild = child;

      return new Option<HandleAndSiblingIndex<int>>(new HandleAndSiblingIndex<int>(child, childIndex));
    }

    public async ValueTask<Option<HandleAndSiblingIndex<int>>> TryGetRootAtAsync(int rootIndex)
    {
      if (rootIndex < 0)
        return default;

      while (_RootIndexes.Count <= rootIndex)
      {
        if (!await ScanForwardAsync().ConfigureAwait(false))
          return default;
      }

      return new Option<HandleAndSiblingIndex<int>>(new HandleAndSiblingIndex<int>(_RootIndexes[rootIndex], rootIndex));
    }

    private bool SpanClosedBehindScan(int handle)
    {
      var subtreeSize = _Store.GetSubtreeSize(handle);

      return subtreeSize > 0 && _ScanCursor >= handle + subtreeSize;
    }

    // Advance the scan by one node: demand it from the store, settle which open spans have
    // closed behind the cursor, and record the node's parent, its link in that parent's
    // sibling chain, and (at stack-empty points) its roothood. False iff the feed exhausted
    // first.
    private async ValueTask<bool> ScanForwardAsync()
    {
      if (_Exhausted)
        return false;

      if (!await _Store.EnsureBufferedAsync(_ScanCursor).ConfigureAwait(false))
      {
        _Exhausted = true;
        return false;
      }

      while (_OpenSpanStack.Count > 0)
      {
        var openSpanStart = _OpenSpanStack.GetLast();
        var openSpanSize = _Store.GetSubtreeSize(openSpanStart);

        if (openSpanSize > 0 && openSpanStart + openSpanSize <= _ScanCursor)
          _OpenSpanStack.RemoveLast();
        else
          break;
      }

      // The node's own chain slots go in first; a parent's linking below touches only
      // earlier slots, which already exist.
      _FirstChildIndexes.AddLast(NoIndex);
      _NextSiblingIndexes.AddLast(NoIndex);
      _LastChildIndexes.AddLast(NoIndex);

      if (_OpenSpanStack.Count == 0)
      {
        _ParentIndexes.AddLast(NoParent);
        _RootIndexes.AddLast(_ScanCursor);
      }
      else
      {
        var parentIndex = _OpenSpanStack.GetLast();

        _ParentIndexes.AddLast(parentIndex);

        if (_FirstChildIndexes[parentIndex] == NoIndex)
          _FirstChildIndexes[parentIndex] = _ScanCursor;
        else
          _NextSiblingIndexes[_LastChildIndexes[parentIndex]] = _ScanCursor;

        _LastChildIndexes[parentIndex] = _ScanCursor;
      }

      _OpenSpanStack.AddLast(_ScanCursor);
      _ScanCursor++;

      return true;
    }
  }
}
