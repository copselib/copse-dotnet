namespace Copse.CodeGen
{
  /// <summary>One async source and the sync twin it transcribes into (paths relative to the <c>src</c> root).</summary>
  public readonly record struct GeneratorEntry(
    string AsyncSource,
    string Twin,
    string AsyncClass,
    string SyncClass,
    string SyncNamespace);

  /// <summary>
  /// The async-source -&gt; generated-sync-twin manifest. Single source of truth for both the regen
  /// tool (Program) and the drift-guard test. Each entry carries the target class name and namespace so
  /// a twin can take over the CANONICAL engine name (e.g. AsyncDepthFirstTreenumerator ->
  /// DepthFirstTreenumerator in Copse.Treenumerators) once the hand-tuned engine is retired, while other
  /// twins stay Generated* until their hand-tuned original is retired.
  /// </summary>
  public static class GeneratorManifest
  {
    public static readonly GeneratorEntry[] Entries =
    {
      // The engines: the twin takes over the CANONICAL name in Copse.Treenumerators (the hand-tuned
      // DepthFirstTreenumerator / BreadthFirstTreenumerator are retired).
      new("Copse.Async/AsyncDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/DepthFirstTreenumerator.g.cs",
        "AsyncDepthFirstTreenumerator", "DepthFirstTreenumerator", "Copse.Treenumerators"),

      new("Copse.Async/AsyncBreadthFirstTreenumerator.cs",
        "Copse/Treenumerators/BreadthFirstTreenumerator.g.cs",
        "AsyncBreadthFirstTreenumerator", "BreadthFirstTreenumerator", "Copse.Treenumerators"),

      new("Copse.Linq.Async/AsyncWhereDepthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedWhereDepthFirstTreenumerator.g.cs",
        "AsyncWhereDepthFirstTreenumerator", "GeneratedWhereDepthFirstTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/AsyncWhereBreadthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedWhereBreadthFirstTreenumerator.g.cs",
        "AsyncWhereBreadthFirstTreenumerator", "GeneratedWhereBreadthFirstTreenumerator", "Copse.Linq.Generated"),
    };
  }
}
