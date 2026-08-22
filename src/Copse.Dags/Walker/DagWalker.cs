using System;
using System.Runtime.CompilerServices;

namespace Copse.Dags
{
  /// <summary>
  /// A place in a dag: the focused pair (topology, handle) -- the comonad's carrier, the
  /// consumer's cursor, and the observer's vantage (the tree family's <c>TreeWalker</c>,
  /// dualized). Steps are EDGE-ATOMIC: every move crosses one edge of one group and the step
  /// answer carries the payload crossed. <see cref="MoveToChild"/> indexes the out-edge group
  /// (downstream), <see cref="MoveToParent"/> indexes the in-edge group (upstream -- the tree's
  /// single parent step, grown an index), <see cref="MoveToSource"/> indexes the virtual
  /// source's child group.
  ///
  /// The UNFOCUSED STANCE is a walker state: standing on the virtual source, above every
  /// source. The door lands there; a source's <c>MoveToParent(0)</c> answers there (the one
  /// upward step a source has -- its seed edge, <c>default</c> payload); stepping up from it is
  /// the algebra's one upward miss; <see cref="Focus"/> and <see cref="GetValue"/> throw there
  /// (the violation channel -- <c>IEnumerator.Current</c> before the first <c>MoveNext</c>),
  /// <see cref="TryGetValue"/> is the typed miss. The empty dag is the unfocused walker alone.
  /// <c>default(DagWalker)</c> is the one invalid inhabitant -- the unfocused stance has a
  /// topology, the default has none.
  ///
  /// The topology is the invariant subject: no member changes it, every step and jump carries it
  /// through. Bulk sweeps belong to the streaming tier and the buffer's arrays; the walker is the
  /// readable vocabulary for neighborhood-priced work.
  /// </summary>
  public readonly struct DagWalker<TValue, THandle, TEdge>
  {
    /// <summary>The focused mint: a walker standing at <paramref name="focus"/>. The trust door -- nothing is validated; a forged handle detonates at the first probe.</summary>
    public DagWalker(IDagTopology<TValue, THandle, TEdge> topology, THandle focus)
    {
      if (topology == null)
        throw new ArgumentNullException(nameof(topology));

      Topology = topology;
      _FocusHandle = focus;
      _HasFocus = true;
    }

    /// <summary>The unfocused mint: the walker standing on the virtual source, above the sources.</summary>
    public DagWalker(IDagTopology<TValue, THandle, TEdge> topology)
    {
      if (topology == null)
        throw new ArgumentNullException(nameof(topology));

      Topology = topology;
      _FocusHandle = default;
      _HasFocus = false;
    }

    public readonly IDagTopology<TValue, THandle, TEdge> Topology;

    // The focus, flattened: the flat pair keeps the struct promotable (the tree walker's
    // measured lesson -- a nested option pads the carrier and every step result copies it).
    private readonly THandle _FocusHandle;
    private readonly bool _HasFocus;

    public bool HasFocus => _HasFocus;

    public THandle Focus
      => _HasFocus ? _FocusHandle : ThrowUnfocusedHasNoHandle();

    /// <summary>Extract: the value at the focus. Throws at the unfocused stance.</summary>
    public TValue GetValue()
      => _HasFocus ? Topology.GetValue(_FocusHandle) : ThrowUnfocusedHasNoValue();

    /// <summary>The lawful extract at every stance: false exactly at the unfocused stance.</summary>
    public bool TryGetValue(out TValue value)
    {
      if (_HasFocus)
      {
        value = Topology.GetValue(_FocusHandle);
        return true;
      }

      value = default;
      return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static THandle ThrowUnfocusedHasNoHandle()
      => throw new InvalidOperationException(
        "The walker is unfocused: it stands on the virtual source, above every source, on no node. Test HasFocus before reading Focus.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TValue ThrowUnfocusedHasNoValue()
      => throw new InvalidOperationException(
        "The walker is unfocused: it stands on the virtual source, above every source, on no node. Test HasFocus, or read TryGetValue, whose miss is typed.");

    /// <summary>The jump: a sibling stance on the same topology at <paramref name="handle"/>. No probe fires; stored handles re-enter here.</summary>
    public DagWalker<TValue, THandle, TEdge> At(THandle handle)
      => new DagWalker<TValue, THandle, TEdge>(Topology, handle);

    /// <summary>
    /// Up one in-edge: in-edge <paramref name="inEdgeIndex"/> of the focus. A source's in-edge
    /// group is empty, so its index-0 step answers the UNFOCUSED walker (the seed edge to the
    /// virtual source); any other index past the group, and every step up from the unfocused
    /// stance, is the miss.
    /// </summary>
    public DagWalkerResult<TValue, THandle, TEdge> MoveToParent(int inEdgeIndex)
    {
      if (!_HasFocus)
        return default;

      var parentStep = Topology.TryGetParentAt(_FocusHandle, inEdgeIndex);

      if (parentStep.HasValue)
        return new DagWalkerResult<TValue, THandle, TEdge>(Topology, parentStep.Handle, parentStep.Edge);

      return inEdgeIndex == 0
        ? new DagWalkerResult<TValue, THandle, TEdge>(Topology)
        : default;
    }

    /// <summary>Down one out-edge: out-edge <paramref name="outEdgeIndex"/> of the focus; from the unfocused stance, source <paramref name="outEdgeIndex"/> (the sources are its child group).</summary>
    public DagWalkerResult<TValue, THandle, TEdge> MoveToChild(int outEdgeIndex)
    {
      if (!_HasFocus)
        return MoveToSource(outEdgeIndex);

      var childStep = Topology.TryGetChildAt(_FocusHandle, outEdgeIndex);

      return childStep.HasValue
        ? new DagWalkerResult<TValue, THandle, TEdge>(Topology, childStep.Handle, childStep.Edge)
        : default;
    }

    /// <summary>To source <paramref name="sourceIndex"/>, from any stance; the miss past the last source.</summary>
    public DagWalkerResult<TValue, THandle, TEdge> MoveToSource(int sourceIndex)
    {
      var sourceStep = Topology.TryGetSourceAt(sourceIndex);

      return sourceStep.HasValue
        ? new DagWalkerResult<TValue, THandle, TEdge>(Topology, sourceStep.Handle, sourceStep.Edge)
        : default;
    }

    public override string ToString()
      => _HasFocus ? $"walker at {_FocusHandle}" : "unfocused walker";
  }
}
