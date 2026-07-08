using System.Threading.Tasks;

namespace Copse.Async
{
  // Async analog of Copse.ILevelOrderStore: the flat family's level-order store protocol for a
  // store that may still be GROWING from an ASYNC feed. The grow operations await; GetFirstChildIndex
  // and GetValue are pure reads of already-buffered data and stay synchronous. See ILevelOrderStore
  // for the full contract (the sync twin the codegen produces).
  public interface IAsyncLevelOrderStore<TValue>
  {
    // Grow the store until root ordinal k exists. False iff the root frontier closed first.
    ValueTask<bool> EnsureRootAvailableAsync(int k);

    // Grow the store until child ordinal k of the (already-available) parent exists. False iff the
    // parent's span closed first.
    ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k);

    // The buffer index of the parent's first child. Only meaningful once the parent has at least
    // one available child.
    int GetFirstChildIndex(int parentIndex);

    TValue GetValue(int index);
  }
}
