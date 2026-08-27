using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Core
{
  /// <summary>
  /// A movable stance on a tree: one node together with the ability to navigate away from it --
  /// or the UNFOCUSED stance, the place above the roots where every walk begins. A walker is an
  /// immutable value: stepping never mutates it, every move returns a new walker, and copying
  /// one is free. All walkers over one source stand on the same topology; do not mutate the
  /// underlying tree while walking it.
  ///
  /// <para>The unfocused stance is a real stance, not an error state: it is where a walker
  /// stands before its first downward step. The roots are its children
  /// (<see cref="MoveToChildAsync"/> walks them from there), climbing up from a root lands on
  /// it, and the only upward miss in the algebra is stepping up from it. It stands on no node,
  /// so it has no handle and no node to read: <see cref="Focus"/> and <see cref="GetNodeAsync"/>
  /// throw there (as <c>IEnumerator.Current</c> throws before the first <c>MoveNext</c>), and
  /// <see cref="TryGetNodeAsync"/> is the read that cannot throw -- absent exactly there.
  /// Test <see cref="HasFocus"/> when a climb may have topped out.</para>
  ///
  /// <para>A <c>default</c> instance has no topology and is invalid; the unfocused stance is
  /// not <c>default</c> -- it carries a topology like any other stance.</para>
  ///
  /// <para>This type carries navigation only: <see cref="GetNodeAsync"/> reads the focused
  /// value, and the step members move the stance. The operator surface over walkers --
  /// <c>Extend</c>, <c>Subtrees</c>, the acquisition methods -- lives in the Linq packages as
  /// extension methods, the same way <c>ITreenumerable</c> lives here while <c>Select</c> and
  /// <c>Where</c> live there.</para>
  ///
  /// <para>Navigation is bidirectional: <see cref="MoveToParentAsync"/> works because a
  /// walker's focus keeps its ancestors, unlike the severed per-subtree view
  /// <c>Subtrees()</c> produces.</para>
  /// </summary>
  public readonly struct AsyncTreeWalker<TNode, THandle>
  {
    /// <summary>Creates a walker standing on <paramref name="focus"/>. For providers
    /// implementing <see cref="IAsyncWalkableTreenumerable{TNode, THandle}"/>; consumers get
    /// walkers from the acquisition methods and from <see cref="At"/>. The handle is not
    /// validated: it is presumed to be a real node of <paramref name="topology"/>, and an
    /// invalid one fails at the first probe.</summary>
    public AsyncTreeWalker(IAsyncTreeTopology<TNode, THandle> topology, THandle focus)
    {
      Topology = topology;
      _FocusHandle = focus;
      _HasFocus = true;
    }

    /// <summary>Creates a walker at the unfocused stance above the roots of
    /// <paramref name="topology"/> -- what <c>GetTreeWalkerAsync</c> returns. Never fails:
    /// the empty forest is simply the unfocused stance with an empty child group.</summary>
    public AsyncTreeWalker(IAsyncTreeTopology<TNode, THandle> topology)
    {
      Topology = topology;
      _FocusHandle = default;
      _HasFocus = false;
    }

    /// <summary>The topology this walker stands on. The two surfaces answer the same
    /// questions in different frames: a topology's methods take a handle and navigate
    /// relative to it; a walker's methods take none and navigate relative to its own stance.
    /// Holding the topology grants no extra power -- every probe is read-only.</summary>
    public readonly IAsyncTreeTopology<TNode, THandle> Topology;

    // The focus, flattened rather than held as an Option<THandle>: the flat pair packs the
    // struct to 16 bytes for ordinal handles (ref + int + bool), where the nested option
    // pads to 24 -- and every step result copies a struct of this shape (measured on the
    // BufferProbes sweeps; WALKER_FACTORY_DESIGN.md §11's perf addendum).
    private readonly THandle _FocusHandle;
    private readonly bool _HasFocus;

    /// <summary>Whether this walker stands on a node. <c>false</c> exactly at the unfocused
    /// stance -- above the roots, no node, no handle. Check it before
    /// <see cref="Focus"/>, and after a climb that may have topped out.</summary>
    public bool HasFocus => _HasFocus;

    /// <summary>The handle of the node this walker stands on. Throws
    /// <see cref="InvalidOperationException"/> at the unfocused stance -- there is no node
    /// there; test <see cref="HasFocus"/> first.</summary>
    public THandle Focus
      => _HasFocus ? _FocusHandle : ThrowUnfocusedHasNoHandle();

    /// <summary>The value of the node this walker stands on. Throws
    /// <see cref="InvalidOperationException"/> at the unfocused stance;
    /// <see cref="TryGetNodeAsync"/> is the read that cannot throw. A method rather than a
    /// property because on a still-growing source the read may pull the source.</summary>
    public ValueTask<TNode> GetNodeAsync()
      => _HasFocus ? Topology.GetNodeAsync(_FocusHandle) : ThrowUnfocusedHasNoNodeAsync();

    /// <summary>The value of the node this walker stands on, or absent at the unfocused
    /// stance -- the one stance with no node to read. On focused stances it agrees with
    /// <see cref="GetNodeAsync"/>.</summary>
    public async ValueTask<Option<TNode>> TryGetNodeAsync()
      => _HasFocus
        ? new Option<TNode>(await Topology.GetNodeAsync(_FocusHandle).ConfigureAwait(false))
        : default;

    // The throw helpers keep `throw new` (allocation + message string) out of the readers'
    // bodies: an inline throw blows the JIT's inline budget on the per-node hot path (the
    // BCL's ThrowHelper pattern; WALKER_FACTORY_DESIGN.md §11's perf addendum).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static THandle ThrowUnfocusedHasNoHandle()
      => throw new InvalidOperationException(
        "The walker is unfocused: it stands above the roots, on no node. Test HasFocus before reading Focus.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ValueTask<TNode> ThrowUnfocusedHasNoNodeAsync()
      => throw new InvalidOperationException(
        "The walker is unfocused: it stands above the roots, on no node. Test HasFocus, or read TryGetNodeAsync, whose miss is typed.");

    /// <summary>A walker on the same topology standing at <paramref name="handle"/> -- how a
    /// stored handle becomes a stance again. No probe fires and nothing is validated: the
    /// handle is presumed to be this topology's own, and an invalid one fails at the first
    /// probe. Always lands on a node -- the unfocused stance has no handle, so
    /// <see cref="At"/> cannot reach it.</summary>
    public AsyncTreeWalker<TNode, THandle> At(THandle handle)
      => new AsyncTreeWalker<TNode, THandle>(Topology, handle);

    /// <summary>Single upward step. From a node with a parent, the parent; from a root, the
    /// UNFOCUSED walker -- that is an answer, not a miss: the climb tops out standing above
    /// the roots; from the unfocused stance, the miss. See
    /// <see cref="AsyncTreeWalkerResult{TNode, THandle}"/> for reading the answer.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TNode, THandle>> MoveToParentAsync()
    {
      if (!_HasFocus)
        return default;

      var parentResult = await Topology.TryGetParentAsync(_FocusHandle).ConfigureAwait(false);

      return parentResult.HasValue
        ? new AsyncTreeWalkerResult<TNode, THandle>(Topology, parentResult.Value)
        : new AsyncTreeWalkerResult<TNode, THandle>(Topology);
    }

    /// <summary>Single downward step to the child at <paramref name="childIndex"/> in sibling
    /// order, or the miss past the last child. From the unfocused stance the children are the
    /// roots, so walking down from where a walk begins needs no special case.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TNode, THandle>> MoveToChildAsync(int childIndex)
    {
      if (!_HasFocus)
        return await MoveToRootAsync(childIndex).ConfigureAwait(false);

      var childResult = await Topology.TryGetChildAtAsync(_FocusHandle, childIndex).ConfigureAwait(false);

      return childResult.HasValue
        ? new AsyncTreeWalkerResult<TNode, THandle>(Topology, childResult.Value.Handle)
        : default;
    }

    /// <summary>A stance at the root at <paramref name="rootIndex"/> in sibling order, from
    /// anywhere on the tree, or the miss past the last root.</summary>
    public async ValueTask<AsyncTreeWalkerResult<TNode, THandle>> MoveToRootAsync(int rootIndex)
    {
      var rootResult = await Topology.TryGetRootAtAsync(rootIndex).ConfigureAwait(false);

      return rootResult.HasValue
        ? new AsyncTreeWalkerResult<TNode, THandle>(Topology, rootResult.Value.Handle)
        : default;
    }

  }
}
