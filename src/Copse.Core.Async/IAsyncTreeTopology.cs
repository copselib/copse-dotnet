using Copse.Core;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// The provider-side interface a tree structure implements so walkers can navigate it: four
  /// probes -- value, parent, child-at-index, root-at-index -- that answer every step a
  /// <c>AsyncTreeWalker</c> can take. Consumers rarely meet this interface directly; they
  /// acquire walkers, and a walker carries its topology.
  ///
  /// <para>Contracts for implementers: <typeparamref name="THandle"/> is your node identity,
  /// on your own terms (store-backed topologies use ordinals) -- the library never compares
  /// <typeparamref name="TValue"/> values. The child axis is indexed, never counted: a probe
  /// past the last child answers an absent <see cref="Option{TValue}"/>, which keeps every
  /// probe finite work regardless of fan-out. The roots are addressed the same way, as their
  /// own indexed group. On a still-growing source, a probe forces the source exactly as far
  /// as its answer requires; upward probes never force.</para>
  /// </summary>
  public interface IAsyncTreeTopology<TValue, THandle>
  {
    /// <summary>The value of the node <paramref name="handle"/> identifies.</summary>
    ValueTask<TValue> GetValueAsync(THandle handle);

    /// <summary>The node's parent, or absent when the node is a root.</summary>
    ValueTask<Option<THandle>> TryGetParentAsync(THandle handle);

    /// <summary>The node's child at <paramref name="childIndex"/> in sibling order, or absent
    /// past the last child.</summary>
    ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetChildAtAsync(THandle handle, int childIndex);

    /// <summary>The root at <paramref name="rootIndex"/> in sibling order, or absent past the
    /// last root.</summary>
    ValueTask<Option<NodeAndSiblingIndex<THandle>>> TryGetRootAtAsync(int rootIndex);
  }
}
