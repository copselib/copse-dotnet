using System;

namespace Copse.Dags
{
  /// <summary>
  /// Where a node's out-edges re-attach inside its expansion (<see cref="DagExpansion{TNode, TEdge}"/>):
  /// the bind's structural slot -- the dag reading of the tree family's pointed expansion,
  /// where the point is a phantom child. A slot is a POSITION, not a node: zero or more
  /// ATTACHMENTS, each hanging the original's out-edges from a fragment node or FROM OUTSIDE
  /// (the original's in-edge parents directly -- promotion, the bypass), each with an optional
  /// payload: absent, the out-edge passes through unchanged (<c>Return</c> rewrites nothing);
  /// present, it composes with the out-edge through the bind's composer -- which is what a
  /// later promotion of a holding node must produce, and why the composer's associativity is
  /// the bind's law. Each attachment may also ANSWER for the out-edges it re-attaches
  /// (<see cref="DagDepartureAnswer{TEdge}"/>: keep, rewrite, suppress -- the dispatching end
  /// owns its edges). More than one attachment is sharing, not duplication: on a dag the slot
  /// can hang from two nodes and the children get two in-edges, which a tree cannot say.
  /// </summary>
  public readonly struct DagSlot<TEdge>
  {
    private DagSlot(DagSlotAttachment<TEdge>[] attachments)
    {
      _Attachments = attachments;
    }

    private readonly DagSlotAttachment<TEdge>[] _Attachments;

    /// <summary>The attachments, in declaration order.</summary>
    public DagSlotAttachment<TEdge>[] Attachments => _Attachments ?? Array.Empty<DagSlotAttachment<TEdge>>();

    public bool IsNone => _Attachments == null || _Attachments.Length == 0;

    /// <summary>No slot: the original's out-edges die (a leaf, or a drop).</summary>
    public static DagSlot<TEdge> None => default;

    /// <summary>The promotion slot: out-edges re-attach to the original's in-edge parents, payloads composed.</summary>
    public static DagSlot<TEdge> Source => new DagSlot<TEdge>(new[] { DagSlotAttachment<TEdge>.FromOutside() });

    /// <summary>Out-edges hang from the given fragment nodes, payloads passed through.</summary>
    public static DagSlot<TEdge> Under(params int[] fragmentNodes)
    {
      if (fragmentNodes == null)
        throw new ArgumentNullException(nameof(fragmentNodes));

      var attachments = new DagSlotAttachment<TEdge>[fragmentNodes.Length];
      for (var index = 0; index < fragmentNodes.Length; index++)
        attachments[index] = DagSlotAttachment<TEdge>.Under(fragmentNodes[index]);

      return new DagSlot<TEdge>(attachments);
    }

    /// <summary>Out-edges hang from the given fragment nodes through the given payloads, composed onto each out-edge.</summary>
    public static DagSlot<TEdge> Under(params (int FragmentNode, TEdge Payload)[] attachments)
    {
      if (attachments == null)
        throw new ArgumentNullException(nameof(attachments));

      var built = new DagSlotAttachment<TEdge>[attachments.Length];
      for (var index = 0; index < attachments.Length; index++)
        built[index] = DagSlotAttachment<TEdge>.Under(attachments[index].FragmentNode, attachments[index].Payload);

      return new DagSlot<TEdge>(built);
    }

    /// <summary>The general slot: any attachments, inside or outside, with or without payloads and answers.</summary>
    public static DagSlot<TEdge> Of(params DagSlotAttachment<TEdge>[] attachments)
      => new DagSlot<TEdge>(attachments == null || attachments.Length == 0 ? null : (DagSlotAttachment<TEdge>[])attachments.Clone());

    /// <summary>The same slot with every attachment answering alike, by index and payload.</summary>
    public DagSlot<TEdge> Answering(Func<int, TEdge, DagDepartureAnswer<TEdge>> answer)
    {
      if (_Attachments == null)
        return this;

      var answering = new DagSlotAttachment<TEdge>[_Attachments.Length];
      for (var index = 0; index < answering.Length; index++)
        answering[index] = _Attachments[index].Answering(answer);

      return new DagSlot<TEdge>(answering);
    }
  }

  /// <summary>
  /// One slot attachment: where the out-edges hang from (a fragment node, or the outside -- the
  /// original's in-edge parents), the optional payload they compose through, and the optional
  /// per-out-edge answers (absent: every out-edge kept as is).
  /// </summary>
  public readonly struct DagSlotAttachment<TEdge>
  {
    private DagSlotAttachment(bool fromOutside, int fragmentNode, bool hasPayload, TEdge payload, Func<int, TEdge, DagDepartureAnswer<TEdge>> answer)
    {
      IsFromOutside = fromOutside;
      FragmentNode = fragmentNode;
      HasPayload = hasPayload;
      Payload = payload;
      _Answer = answer;
    }

    private readonly Func<int, TEdge, DagDepartureAnswer<TEdge>> _Answer;

    public readonly bool IsFromOutside;
    public readonly int FragmentNode;
    public readonly bool HasPayload;
    public readonly TEdge Payload;

    /// <summary>The answer for out-edge <paramref name="outEdgeIndex"/> with payload <paramref name="payload"/>: keep when the attachment does not answer.</summary>
    public DagDepartureAnswer<TEdge> Answer(int outEdgeIndex, TEdge payload) => _Answer == null ? DagDepartureAnswer<TEdge>.Keep : _Answer(outEdgeIndex, payload);

    public bool Answers => _Answer != null;

    public static DagSlotAttachment<TEdge> Under(int fragmentNode) => new DagSlotAttachment<TEdge>(false, fragmentNode, false, default, null);
    public static DagSlotAttachment<TEdge> Under(int fragmentNode, TEdge payload) => new DagSlotAttachment<TEdge>(false, fragmentNode, true, payload, null);
    public static DagSlotAttachment<TEdge> FromOutside() => new DagSlotAttachment<TEdge>(true, -1, false, default, null);
    public static DagSlotAttachment<TEdge> FromOutside(TEdge payload) => new DagSlotAttachment<TEdge>(true, -1, true, payload, null);

    /// <summary>The same attachment answering for the out-edges it re-attaches, by index and payload -- a LOCAL function (the node's own out-edge order and payloads), which is what keeps the bind's law.</summary>
    public DagSlotAttachment<TEdge> Answering(Func<int, TEdge, DagDepartureAnswer<TEdge>> answer)
      => new DagSlotAttachment<TEdge>(IsFromOutside, FragmentNode, HasPayload, Payload, answer);

    public override string ToString()
    {
      var origin = IsFromOutside ? "outside" : $"under {FragmentNode}";
      var via = HasPayload ? $"{origin} via {Payload}" : origin;
      return _Answer == null ? via : $"{via} answering";
    }
  }
}
