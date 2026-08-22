namespace Copse.Dags
{
  /// <summary>The payload of an edge that carries nothing -- the subdivision's edges, and any edge-less dag spelled <c>&lt;TNode, Unit&gt;</c>.</summary>
  public readonly struct Unit
  {
    public static readonly Unit Value = default;

    /// <summary>The composer for unit payloads: nothing composed with nothing.</summary>
    public static Unit Compose(Unit upstream, Unit downstream) => default;

    public override string ToString() => "()";
  }
}
