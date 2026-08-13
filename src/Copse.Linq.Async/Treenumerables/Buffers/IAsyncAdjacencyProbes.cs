using Copse.Async;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The four walkable probes over ordinal handles, as an internal seam: buffer classes hold
  // this polymorphically (a memo picks its engine by pinned layout at first probe) and the
  // two adjacency engines implement it per store layout. Probe semantics are the walkable
  // contract's (docs/WALKABLE_CONTRACT_DESIGN.md): a probe on a growing store is DEMAND and
  // forces the feed exactly as far as the answer needs; a probe past a retired feed surfaces
  // the store's own lifecycle behavior (ObjectDisposedException -- the memo replay rule,
  // inherited rather than reimplemented).
  internal interface IAsyncAdjacencyProbes<TValue>
  {
    ValueTask<TValue> GetValueAsync(int handle);
    ValueTask<ParentResult<int>> GetParentAsync(int handle);
    ValueTask<ChildResult<int>> GetChildAtAsync(int handle, int childIndex);
    ValueTask<ChildResult<int>> GetRootAtAsync(int rootIndex);
  }
}
