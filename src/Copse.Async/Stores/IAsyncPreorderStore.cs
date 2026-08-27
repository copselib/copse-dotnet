using System.Threading.Tasks;

namespace Copse.Stores
{
  // Codegen source of the sync twin, Copse.Stores.IPreorderStore.
  /// <summary>
  /// The preorder store protocol: random access to a tree encoded as a preorder array, for a
  /// store that may still be growing from an async feed. The grow operations await, pulling
  /// the underlying feed just far enough to answer (a completed capture satisfies them
  /// immediately); the reads are synchronous over already-buffered data -- callers ensure
  /// before they read.
  /// </summary>
  public interface IAsyncPreorderStore<TNode>
  {
    /// <summary>Grows the store until the node at <paramref name="index"/> exists. Completes
    /// with <c>false</c> when the underlying feed exhausts first (no such node).</summary>
    ValueTask<bool> EnsureBufferedAsync(int index);

    /// <summary>Grows the store until node <paramref name="index"/>'s subtree closes, and
    /// completes with its size (at least 1). The node itself must already be
    /// buffered.</summary>
    ValueTask<int> EnsureSubtreeClosedAsync(int index);

    /// <summary>The size of node <paramref name="index"/>'s subtree, or 0 while that subtree
    /// is still open (a closed subtree's size is at least 1).</summary>
    int GetSubtreeSize(int index);

    /// <summary>The value at <paramref name="index"/>, which must already be
    /// buffered.</summary>
    TNode GetNode(int index);
  }
}
