using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Stores
{
  // A COMPLETED preorder store over plain arrays: values[i] in preorder, node i's subtree
  // spanning [i, i + subtreeSizes[i]). The trivial implementation of the store protocol --
  // nothing grows, so the Ensure* hooks answer with completed results and the reads are plain
  // array access. Wrapped in a treenumerable this is a full citizen (random access affords both
  // dimensions). Each color owns its own: the sync twin is generated from this file, so the two
  // never share an assembly and the neutral layer stays store-free.
  //
  // Taxonomy (design-docs/STORE_FAMILY_REVIEW.md): preorder x completed x no feed.
  /// <summary>A completed preorder store over plain arrays: <c>nodes[i]</c> in preorder,
  /// node <c>i</c>'s subtree spanning <c>[i, i + subtreeSizes[i])</c>. Nothing grows, so the
  /// grow operations answer immediately and the reads are array access.</summary>
  public readonly struct AsyncPreorderArrayStore<TNode> : IAsyncPreorderStore<TNode>
  {
    /// <summary>Wraps the completed arrays: <c>nodes[i]</c> in preorder with node <c>i</c>'s
    /// subtree size beside it. The arrays are taken by reference, not copied.</summary>
    public AsyncPreorderArrayStore(TNode[] nodes, int[] subtreeSizes)
    {
      _Nodes = nodes;
      _SubtreeSizes = subtreeSizes;
    }

    private readonly TNode[] _Nodes;
    private readonly int[] _SubtreeSizes;

    /// <inheritdoc/>
    public int Count => _Nodes.Length;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> EnsureBufferedAsync(int index) => new ValueTask<bool>(index < _Nodes.Length);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> EnsureSubtreeClosedAsync(int index) => new ValueTask<int>(_SubtreeSizes[index]);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSubtreeSize(int index) => _SubtreeSizes[index];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TNode GetNode(int index) => _Nodes[index];
  }
}
