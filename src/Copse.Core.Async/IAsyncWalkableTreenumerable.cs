using Copse.Core;
using System.Threading.Tasks;

namespace Copse.Core
{
  /// <summary>
  /// A treenumerable that also affords navigation: from any node's handle, single steps to its
  /// parent, its children, and the roots, without enumerating the tree. Whether a source can be
  /// navigated is a compile-time fact -- a deferred pipeline is not walkable, and
  /// <c>Materialize()</c> is the explicit upgrade that returns a walkable capture.
  ///
  /// <para><typeparamref name="THandle"/> is the handle type: a node's identity within this
  /// source -- an ordinal for a store-backed source, the node itself where the node is its own
  /// identity. Handles are compared by the provider on its own terms;
  /// <typeparamref name="TNode"/> values are never compared. Handle spaces are per-capture:
  /// two captures of the same tree, or the same tree under two layouts, are foreign to each
  /// other, and handles never travel between sources.</para>
  ///
  /// <para>The child axis is indexed, not enumerated: probes answer by value, and there is
  /// deliberately no child count on the contract -- a probe is finite work per call whatever
  /// the fan-out, where a count diverges on a source with an unbounded child group. To count
  /// children, probe to the first miss; concrete types with cheap counts expose them as their
  /// own members.</para>
  ///
  /// <para>Navigation presumes a stable tree: do not navigate across a mutation of the
  /// underlying source. On a still-growing source a probe pulls the source exactly as far as
  /// its answer requires; a completed source answers immediately.</para>
  /// </summary>
  // THE CHARTER (design-docs/WALKER_FACTORY_DESIGN.md, Stage C + §11): ITreenumerable is
  // an enumerator factory; IWalkableTreenumerable is a TREE WALKER factory. One member,
  // and it cannot miss -- both factories are total. The probes live on
  // IAsyncTreeTopology -- the provider SPI, which this contract does not expose to
  // consumers: the walker is the entire public navigation surface, the topology is bound
  // at the door, and the walkable appears in no navigation call path.
  public interface IAsyncWalkableTreenumerable<TNode, THandle> : IAsyncTreenumerable<TNode>
  {
    /// <summary>
    /// A walker at the unfocused stance -- above the roots, before the first downward step --
    /// exactly as <c>GetEnumerator</c> hands back a cursor at the before-first position.
    /// Never fails: the empty forest is the unfocused stance with an empty child group. Walk
    /// down with <c>MoveToChildAsync</c> (from there, the children are the roots).
    /// </summary>
    ValueTask<AsyncTreeWalker<TNode, THandle>> GetTreeWalkerAsync();
  }
}
