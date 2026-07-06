namespace Copse.CodeGen
{
  /// <summary>
  /// The async-source -&gt; generated-sync-twin pairs (paths relative to the <c>src</c> root). Single
  /// source of truth for both the regen tool (Program) and the drift-guard test, so they can never
  /// disagree about which twins exist.
  /// </summary>
  public static class GeneratorManifest
  {
    public static readonly (string AsyncSource, string GeneratedTwin)[] Pairs =
    {
      ("Copse.Async/AsyncDepthFirstTreenumerator.cs",
        "Copse/Generated/GeneratedDepthFirstTreenumerator.g.cs"),
      ("Copse.Async/AsyncBreadthFirstTreenumerator.cs",
        "Copse/Generated/GeneratedBreadthFirstTreenumerator.g.cs"),
      ("Copse.Linq.Async/AsyncWhereDepthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedWhereDepthFirstTreenumerator.g.cs"),
      ("Copse.Linq.Async/AsyncWhereBreadthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedWhereBreadthFirstTreenumerator.g.cs"),
    };
  }
}
