using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// A treenumerable that also affords ADJACENCY: single-step navigation from a handle to its
  /// parent and children, without enumerating the tree. The walkable rung of the capability ladder
  /// the traversal-dimension split established -- narrow (one traversal dimension) to composite
  /// (both) to walkable (both, plus adjacency) -- so "can this source be navigated" is a
  /// compile-time fact, and asking a deferred pipeline for adjacency is a compile error rather than
  /// a hidden materialization. See design-docs/WALKER_DESIGN.md and design-docs/WALKABLE_CONTRACT_DESIGN.md.
  ///
  /// <para><typeparamref name="THandle"/> is the HANDLE type -- an ordinal for a store-backed
  /// source, the value itself for a source whose value is its own handle. Handles are compared by
  /// the provider on its own terms (ordinals by index, references by identity); the library's
  /// no-node-equality pledge is preserved -- <typeparamref name="TValue"/> is never compared.
  /// Handle spaces are PER-CAPTURE: two captures of the same tree, or the same tree under two
  /// layouts, are foreign to each other, and handles never travel between sources.</para>
  ///
  /// <para>The child axis is INDEXED, not enumerated (the VisualTreeHelper shape): every probe
  /// returns BY VALUE, so no adjacency call can allocate -- the measured lesson that put
  /// <c>TChildEnumerator</c> on the engine (interface-typed child enumerators heap-allocate per
  /// node and tank sweeps) is satisfied here by construction, with no third type parameter to
  /// carry it. There is deliberately NO child count on the contract: a probe is finite work per
  /// call whatever the fan-out, but a count diverges on a generator-backed provider with an
  /// unbounded child group. Counting is a derived query (walk the probe to the first miss);
  /// finite providers expose cheap counts as members of their concrete types. The engine's
  /// child-enumerator pull protocol is untouched -- that is the hierarchical family's source
  /// adapter, a different job.</para>
  ///
  /// <para>The probes return options (the Try is built into the shape: an absent value past
  /// the end), and the parent axis is the same option over a bare handle. Roots are the
  /// children of the virtual forest-root
  /// position (the pre-enumeration convention, reused as the walker's starting position above all
  /// roots), which is why they are indexed like any other child group -- the entry probe is the
  /// protocol door, and a source having one is the job description, not a smell (the resolved
  /// TryGetRootAt finding, WALKER_DESIGN.md).</para>
  ///
  /// <para>Adjacency answers are relative to a STABLE topology: implementations must not mutate
  /// their source, and callers must not navigate across a mutation of it. A probe on a growing
  /// source is DEMAND -- it may pull the underlying feed just far enough to answer (the
  /// grow-precedes-read protocol); a completed source answers immediately.</para>
  /// </summary>
  // THE CHARTER, final form (Stage C of design-docs/WALKER_FACTORY_DESIGN.md, the cut;
  // the door made TOTAL by §11, the sentinel completion): ITreenumerable is an enumerator
  // factory; IWalkableTreenumerable is a TREE WALKER factory. One member, and it cannot
  // miss -- both factories are total. The probes live on IAsyncTreeTopology -- the
  // provider SPI, which this contract does not expose to consumers: the walker is the
  // entire public navigation surface, the topology is bound at the door, and the walkable
  // appears in no navigation call path. BREAKING (pre-beta, release-notes flag): the door
  // was result-typed (`TryGetTreeWalkerAsync`) when the empty forest was a miss; the unfocused
  // stance made emptiness an answer, so the Try exits per the grammar.
  public interface IAsyncWalkableTreenumerable<TValue, THandle> : IAsyncTreenumerable<TValue>
  {
    /// <summary>
    /// The door: a walker at the UNFOCUSED STANCE -- above the roots, before the first downward
    /// step -- exactly as <c>GetEnumerator</c> hands back a machine at the before-first
    /// position. Total: the empty forest is the unfocused stance alone (its child group is
    /// empty), so there is nothing here to miss. The factory binds the walker's TOPOLOGY at
    /// birth -- the best physics this source affords (a capture hands its adjacency index,
    /// a memo its pull-through, a lens its rewritten view) -- and then exits the story: the
    /// walkable appears in no navigation call path, exactly as <c>IEnumerable</c> after
    /// <c>GetEnumerator</c>.
    /// </summary>
    ValueTask<AsyncTreeWalker<TValue, THandle>> GetTreeWalkerAsync();
  }
}
