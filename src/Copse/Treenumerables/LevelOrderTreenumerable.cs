using Copse.Core;
using Copse.Treenumerators;

namespace Copse.Treenumerables
{
  /// <summary>
  /// A tree stored in flat level-order form: <see cref="PreorderTreenumerable{TValue, TStore}"/>'s
  /// structural dual. Any <see cref="ILevelOrderStore{TValue}"/> becomes a full
  /// <see cref="ITreenumerable{TValue}"/>: breadth-first traversal is native playback (a
  /// sequential read), depth-first rides the same store cross-order (child-span descent; the
  /// accepted locality tax). See TRAVERSAL_DIMENSION_SPLIT.md -- a random-access store buys full
  /// citizenship; forward-only sources get narrower types.
  /// </summary>
  public sealed class LevelOrderTreenumerable<TValue, TStore> : ITreenumerable<TValue>
    where TStore : ILevelOrderStore<TValue>
  {
    public LevelOrderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new LevelOrderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new LevelOrderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);
  }
}
