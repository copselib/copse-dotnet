using Copse;

namespace Copse.Linq.Treenumerables
{
  // The named intersection of two ORTHOGONAL capabilities -- the finiteness law in the type
  // system (docs/WALKER_DESIGN.md): IWalkableTreenumerable says ADJACENCY (you can navigate; no
  // finiteness claim -- a native-adjacency walker may serve an infinite structure), and
  // ITreenumerableBuffer says CAPTURE (owned, in-memory, finite, effect-free replay; the O(n)
  // paid or pinned). Their intersection is the interchange citizen: what the finite-izing
  // escalation (MaterializeWalkable) returns, so the signature itself says "adjacency was
  // manufactured AND the structure was captured." Native-adjacency providers implement the
  // walkable interface alone -- the type's SILENCE about buffer-ness is the infinity
  // permission -- and termination-hungry operations (a height, a whole-structure reify) may
  // constrain on this intersection to make "diverges on infinite trees" a compile error.
  //
  // Like ITreenumerableBuffer itself, a declared capability with stated laws, not a proof: an
  // implementation over an infinite structure is out of contract, not merely exotic.
  // Sync-only for the walker PoC; the async twin arrives when the walker crosses colors.
  public interface IWalkableTreenumerableBuffer<TValue, THandle>
    : IWalkableTreenumerable<TValue, THandle>, ITreenumerableBuffer<TValue>
  {
  }
}
