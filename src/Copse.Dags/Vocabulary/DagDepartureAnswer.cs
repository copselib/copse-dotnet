namespace Copse.Dags
{
  /// <summary>
  /// The bind's answer for one of the original's out-edges as it re-attaches at a slot
  /// attachment: keep it (the default), rewrite its payload, or suppress it. The dispatching end
  /// owns the edge, so answers live on the expansion of the node the edge leaves. LAWFUL IN
  /// THE PROMOTION-FREE FRAGMENT: an answer that depends on index or payload cannot be fused
  /// past an earlier pass's promotion beneath it, because promotion composes a suffix onto the
  /// edge the answer would have to see and locality forbids reading it (pinned as a
  /// principled non-law). Answering then promoting, and a promotion answering its own
  /// departures, associate. A child reads its arrivals as the parents left them; it never rewrites
  /// them from its own seat in the bind -- in-edge rewriting is the transpose-conjugate, or an
  /// extend's business (<c>SelectInEdges</c>).
  /// </summary>
  public readonly struct DagDepartureAnswer<TEdge>
  {
    private DagDepartureAnswer(byte kind, TEdge payload)
    {
      _Kind = kind;
      Payload = payload;
    }

    private readonly byte _Kind;

    public readonly TEdge Payload;

    public bool IsKeep => _Kind == 0;
    public bool IsRewrite => _Kind == 1;
    public bool IsSuppress => _Kind == 2;

    public static DagDepartureAnswer<TEdge> Keep => default;
    public static DagDepartureAnswer<TEdge> Rewrite(TEdge payload) => new DagDepartureAnswer<TEdge>(1, payload);
    public static DagDepartureAnswer<TEdge> Suppress => new DagDepartureAnswer<TEdge>(2, default);

    public override string ToString() => IsKeep ? "keep" : IsSuppress ? "suppress" : $"rewrite {Payload}";
  }
}
