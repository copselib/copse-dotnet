namespace Copse.Dags
{
  /// <summary>
  /// A step's answer: the walker after the step plus the edge payload it crossed, or the miss.
  /// Three outcomes as values rather than bits, so only legal states are spellable: the miss
  /// (<c>default</c> -- <c>None</c> is zero), the UNFOCUSED answer (a source stepped up to the
  /// virtual source; <see cref="Edge"/> is the seed edge's <c>default</c>), and the FOCUSED
  /// answer. Flat fields, no nested option (the tree family's measured promotion cliff);
  /// internal mints -- only the family produces step answers; an option-shaped surface.
  /// </summary>
  public readonly struct DagWalkerResult<TValue, THandle, TEdge>
  {
    private enum StepOutcome : byte
    {
      None = 0,
      Unfocused = 1,
      Focused = 2,
    }

    internal DagWalkerResult(IDagTopology<TValue, THandle, TEdge> topology, THandle handle, TEdge edge)
    {
      _Topology = topology;
      _Handle = handle;
      _Edge = edge;
      _Result = StepOutcome.Focused;
    }

    internal DagWalkerResult(IDagTopology<TValue, THandle, TEdge> topology)
    {
      _Topology = topology;
      _Handle = default;
      _Edge = default;
      _Result = StepOutcome.Unfocused;
    }

    private readonly IDagTopology<TValue, THandle, TEdge> _Topology;
    private readonly THandle _Handle;
    private readonly TEdge _Edge;
    private readonly StepOutcome _Result;

    public bool HasValue => _Result != StepOutcome.None;

    /// <summary>The payload of the edge the step crossed; <c>default</c> on the miss and on the seed edge to the virtual source.</summary>
    public TEdge Edge => _Edge;

    public DagWalker<TValue, THandle, TEdge> Value
    {
      get
      {
        switch (_Result)
        {
          case StepOutcome.Focused:
            return new DagWalker<TValue, THandle, TEdge>(_Topology, _Handle);
          case StepOutcome.Unfocused:
            return new DagWalker<TValue, THandle, TEdge>(_Topology);
          default:
            return default;
        }
      }
    }

    public bool TryGetValue(out DagWalker<TValue, THandle, TEdge> walker)
    {
      walker = Value;

      return HasValue;
    }

    public override string ToString()
    {
      switch (_Result)
      {
        case StepOutcome.Focused:
          return $"walker at {_Handle} via {_Edge}";
        case StepOutcome.Unfocused:
          return "unfocused walker";
        default:
          return "none";
      }
    }
  }
}
