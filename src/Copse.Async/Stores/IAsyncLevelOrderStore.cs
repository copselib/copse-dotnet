using System.Threading.Tasks;

namespace Copse.Stores
{
  // Codegen source of the sync twin, Copse.Stores.ILevelOrderStore.
  /// <summary>
  /// The level-order store protocol: random access to a tree encoded as a level-order array,
  /// for a store that may still be growing from an async feed. The grow operations await,
  /// pulling the underlying feed just far enough to answer; the reads are synchronous over
  /// already-buffered data -- callers ensure before they read.
  /// </summary>
  public interface IAsyncLevelOrderStore<TNode>
  {
    /// <summary>Grows the store until root ordinal <paramref name="k"/> exists. Completes with
    /// <c>false</c> when the root group closed first.</summary>
    ValueTask<bool> EnsureRootAvailableAsync(int k);

    /// <summary>Grows the store until child ordinal <paramref name="k"/> of the
    /// already-available parent at <paramref name="parentIndex"/> exists. Completes with
    /// <c>false</c> when the parent's child group closed first.</summary>
    ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k);

    /// <summary>The buffer index of the parent's first child. Meaningful only once the parent
    /// has at least one available child.</summary>
    int GetFirstChildIndex(int parentIndex);

    /// <summary>The value at <paramref name="index"/>, which must already be
    /// buffered.</summary>
    TNode GetNode(int index);
  }
}
