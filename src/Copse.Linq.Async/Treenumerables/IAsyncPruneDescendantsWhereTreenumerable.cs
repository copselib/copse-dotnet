using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  /// <summary>
  /// The PUBLIC prune-after citizenship
  /// (design-docs/PUBLIC_COMPOSITION_SURFACE_DESIGN.md): a treenumerable that can compose
  /// a prune-after -- keep each matching node, shed its subtree -- into its own machinery
  /// instead of being wrapped. <c>PruneDescendantsWhere</c> probes for this citizenship, so
  /// implementing this interface is the ENTIRE act of joining -- no registration, no
  /// cooperation from this library.
  ///
  /// <para>Prune-after is public where filtering is not because its whole rewrite is a
  /// consumer-protocol primitive: <c>PruneDescendants</c>, forwarded to the walk. The kept
  /// node keeps its position and no sibling of anyone renumbers -- there is no relabeling
  /// for an implementation to get wrong (the boundary law: public composition is what
  /// consumer strategies can express).</para>
  ///
  /// <para>The contract is deliberately minimal and FINAL (the supported older TFMs have
  /// no default interface members, so any member added later would break every
  /// implementer): one method, value-predicate flavor only -- the positional PruneDescendantsWhere
  /// flavor takes the wrapper over citizens.</para>
  ///
  /// <para>Implementation note (the absorption claim): fold the predicate into your own
  /// walk -- forward <c>PruneDescendants</c> where it matches -- and return the rebuilt
  /// citizen, or return the library's prune wrapper over your source. Never implement
  /// this by calling the <c>PruneDescendantsWhere</c> extension on yourself: the extension defers to
  /// this method, so that spelling is mutual recursion.</para>
  /// </summary>
  public interface IAsyncPruneDescendantsWhereTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    /// <summary>
    /// Compose <paramref name="predicate"/> into this treenumerable's own machinery:
    /// every matching node is kept and its descendants are shed, exactly as the
    /// <c>PruneDescendantsWhere</c> operator behaves over this source. The return type IS the
    /// citizenship: closure in this capability is enforced by the signature.
    /// </summary>
    IAsyncPruneDescendantsWhereTreenumerable<TNode> ComposePruneDescendantsWhere(Func<TNode, bool> predicate);
  }
}
