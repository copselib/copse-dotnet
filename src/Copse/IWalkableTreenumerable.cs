using Copse.Core;

namespace Copse
{
  /// <summary>
  /// A treenumerable that also affords ADJACENCY: single-step navigation from a node handle to its
  /// parent and children, without enumerating the tree. The walkable rung of the capability ladder
  /// the traversal-dimension split established -- narrow (one traversal dimension) to composite
  /// (both) to walkable (both, plus adjacency) -- so "can this source be navigated" is a
  /// compile-time fact, and asking a deferred pipeline for adjacency is a compile error rather than
  /// a hidden materialization. See docs/WALKER_DESIGN.md.
  ///
  /// <para><typeparamref name="TNode"/> is the HANDLE type -- an ordinal for a store-backed
  /// source, the node itself for a source whose node is its surfaced value (the same split, and
  /// the same degenerate case, as <c>Treenumerable&lt;TValue, TNode, TChildEnumerator&gt;</c>).
  /// Handles are compared by the provider on its own terms (ordinals by index, references by
  /// identity); the library's no-node-equality pledge is preserved -- <typeparamref name="TValue"/>
  /// is never compared.</para>
  ///
  /// <para>The child axis is INDEXED, not enumerated (the VisualTreeHelper shape:
  /// GetChildrenCount + GetChild(parent, index)): every member returns BY VALUE, so no adjacency
  /// call can allocate -- the measured lesson that put <c>TChildEnumerator</c> on the engine
  /// (interface-typed child enumerators heap-allocate per node and tank sweeps) is satisfied here
  /// by construction, with no third type parameter to carry it. The flat family affords the shape
  /// natively: the level-order store protocol is already count-probe plus first-child offset, and
  /// the preorder walkable rides a lazily built CSR child index (one O(n) pass, ~2n ints -- the
  /// honest-O(1)-indexer precedent) beside its lazy parent index. The engine's
  /// <see cref="IChildEnumerator{TNode}"/> pull protocol is untouched -- that is the hierarchical
  /// family's source adapter, a different job.</para>
  ///
  /// <para>The probes return <see cref="ChildResult{TNode}"/> (the Try is built into the shape:
  /// HasChild false past the end); the parent axis mirrors it with
  /// <see cref="ParentResult{TNode}"/>. Roots are the children of the virtual forest-root position
  /// (<c>NodePosition.ForestRoot</c> -- the pre-enumeration convention, reused as the walker's
  /// starting position above all roots), which is why they are indexed like any other child
  /// group.</para>
  ///
  /// <para>Adjacency answers are relative to a STABLE topology: implementations must not mutate
  /// their source, and callers must not navigate across a mutation of it.</para>
  /// </summary>
  public interface IWalkableTreenumerable<TValue, TNode> : ITreenumerable<TValue>
  {
    /// <summary>Resolves a handle to its surfaced value (the interface form of the engine's
    /// node-to-value map; the identity when TNode is its own value).</summary>
    TValue GetValue(TNode node);

    /// <summary>Single upward step. <c>HasParent</c> is false iff the node is a root.</summary>
    ParentResult<TNode> GetParent(TNode node);

    /// <summary>Indexed downward probe: the node's child at <paramref name="childIndex"/> in
    /// sibling order, or <c>HasChild</c> false past the last child.</summary>
    ChildResult<TNode> GetChildAt(TNode node, int childIndex);

    /// <summary>The virtual forest-root's child group: root <paramref name="rootIndex"/> in
    /// sibling order, or <c>HasChild</c> false past the last root. The walker's entry point when
    /// no handle is in hand yet.</summary>
    ChildResult<TNode> GetRootAt(int rootIndex);

    // Deliberately NO GetChildCount: a probe is finite work per call whatever the fan-out, but a
    // count diverges on a generator-backed provider with an unbounded child group (the
    // VisualTreeHelper shape minus its finite-UI-tree assumption). Counting is a derived query
    // (walk the probe to the first miss -- the LINQ Count() contract, divergent on infinite
    // sequences by the caller's choice); finite providers expose cheap counts as members of
    // their concrete types.
  }
}
