using Copse.Core;
using Copse.Treenumerators;

namespace Copse.Treenumerables
{
  /// <summary>
  /// A tree stored in flat preorder form: the flat family's counterpart of
  /// <see cref="Treenumerable{TValue, TNode, TChildEnumerator}"/> (which adapts hierarchically
  /// stored trees via the child-enumerator protocol). Any <see cref="IPreorderStore{TValue}"/> --
  /// completed arrays, a growing capture, a lazily parsed serialized source -- becomes a full
  /// <see cref="ITreenumerable{TValue}"/>: depth-first traversal is native playback (a
  /// sequential read), breadth-first rides the same store cross-order (span hops; the accepted
  /// locality tax). See TRAVERSAL_DIMENSION_SPLIT.md -- a random-access store buys full
  /// citizenship; forward-only sources get narrower types.
  /// </summary>
  public sealed class PreorderTreenumerable<TValue, TStore> : ITreenumerable<TValue>
    where TStore : IPreorderStore<TValue>
  {
    public PreorderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new PreorderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new PreorderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);
  }
}
