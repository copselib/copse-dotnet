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
  /// <para>The child axis reuses the engine's pull protocol (<see cref="IChildEnumerator{TNode}"/>
  /// over <see cref="ChildResult{TNode}"/>); the parent axis mirrors it with
  /// <see cref="ParentResult{TNode}"/>. Roots are the children of the virtual forest-root position
  /// (<c>NodePosition.ForestRoot</c> -- the pre-enumeration convention, reused as the walker's
  /// starting position above all roots), which is why they are exposed as a child enumerator
  /// rather than a collection.</para>
  ///
  /// <para>Adjacency answers are relative to a STABLE topology: implementations must not mutate
  /// their source, and callers must not navigate across a mutation of it.</para>
  /// </summary>
  public interface IWalkableTreenumerable<TValue, TNode, TChildEnumerator> : ITreenumerable<TValue>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    /// <summary>Resolves a handle to its surfaced value (the interface form of the engine's
    /// node-to-value map; the identity when TNode is its own value).</summary>
    TValue GetValue(TNode node);

    /// <summary>Single upward step. <c>HasParent</c> is false iff the node is a root.</summary>
    ParentResult<TNode> GetParent(TNode node);

    /// <summary>Single-node downward pull: the node's children, in sibling order.</summary>
    TChildEnumerator GetChildEnumerator(TNode node);

    /// <summary>The children of the virtual forest-root position: the roots, in sibling order.
    /// The walker's entry point when no handle is in hand yet.</summary>
    TChildEnumerator GetRootEnumerator();
  }
}
