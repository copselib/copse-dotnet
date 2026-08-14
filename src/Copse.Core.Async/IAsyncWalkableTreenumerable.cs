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
  /// a hidden materialization. See docs/WALKER_DESIGN.md and docs/WALKABLE_CONTRACT_DESIGN.md.
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
  /// <para>The probes return <see cref="ChildResult{TNode}"/> (the Try is built into the shape:
  /// HasChild false past the end); the parent axis mirrors it with
  /// <see cref="ParentResult{THandle}"/>. Roots are the children of the virtual forest-root
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
  // STAGE A of the walker factory design (docs/WALKER_FACTORY_DESIGN.md): the charter --
  // ITreenumerable is an enumerator factory; IWalkableTreenumerable is a TREE WALKER
  // factory. The four probes now live on IAsyncTreeTopology (the provider SPI this contract
  // inherits); the door below is the contract's own affordance. Stage C removes the
  // topology inheritance from this PUBLIC contract (the probes stay SPI-reachable for
  // providers; consumers keep only the door and the walker it manufactures).
  public interface IAsyncWalkableTreenumerable<TValue, THandle>
    : IAsyncTreenumerable<TValue>, IAsyncTreeTopology<TValue, THandle>
  {
    /// <summary>
    /// The door: a walker standing at the first root, or an empty result for the empty
    /// forest (the honest miss; the no-unfocused-walker invariant kept at the door). The
    /// factory binds the walker's TERRAIN at birth -- the best physics this source affords
    /// (a capture hands its adjacency index, a memo its pull-through, a lens its rewritten
    /// view) -- and then exits the story: the walkable appears in no navigation call path,
    /// exactly as <c>IEnumerable</c> after <c>GetEnumerator</c>.
    /// </summary>
    ValueTask<AsyncTreeWalkerResult<TValue, THandle>> TryGetTreeWalkerAsync();
  }
}
