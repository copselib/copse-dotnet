using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // A COMPLETED level-order store over plain arrays: nodes in level order, each node's child
  // span described by firstChildIndices[i] + childCounts[i], the roots the leading rootCount
  // entries. The structural dual of AsyncPreorderArrayStore -- nothing grows, the Ensure* hooks
  // answer with completed results, the reads are plain array access. Each color owns its own:
  // the sync twin is generated from this file.
  //
  // Taxonomy (design-docs/STORE_FAMILY_REVIEW.md): level-order x completed x no feed.
  /// <summary>A completed level-order store over plain arrays: nodes level by level, with
  /// each node's first-child index and child count beside them. Nothing grows, so the grow
  /// operations answer immediately and the reads are array access.</summary>
  public readonly struct AsyncLevelOrderArrayStore<TNode> : IAsyncLevelOrderStore<TNode>
  {
    public AsyncLevelOrderArrayStore(TNode[] nodes, int[] firstChildIndices, int[] childCounts, int rootCount)
    {
      _Nodes = nodes;
      _FirstChildIndices = firstChildIndices;
      _ChildCounts = childCounts;
      _RootCount = rootCount;
    }

    private readonly TNode[] _Nodes;
    private readonly int[] _FirstChildIndices;
    private readonly int[] _ChildCounts;
    private readonly int _RootCount;

    public int Count => _Nodes.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> EnsureRootAvailableAsync(int k) => new ValueTask<bool>(k < _RootCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k) => new ValueTask<bool>(k < _ChildCounts[parentIndex]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TNode GetNode(int index) => _Nodes[index];
  }
}
