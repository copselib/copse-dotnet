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
      new("Copse.Async/Treenumerators/AsyncDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/DepthFirstTreenumerator.g.cs",
        "AsyncDepthFirstTreenumerator", "DepthFirstTreenumerator", "Copse.Treenumerators"),
      new("Copse.Async/Treenumerators/AsyncPreorderStoreDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/PreorderStoreDepthFirstTreenumerator.g.cs",
        "AsyncPreorderStoreDepthFirstTreenumerator", "PreorderStoreDepthFirstTreenumerator", "Copse.Treenumerators"),
      new("Copse.Async/Treenumerators/AsyncPreorderStoreBreadthFirstTreenumerator.cs",
        "Copse/Treenumerators/PreorderStoreBreadthFirstTreenumerator.g.cs",
        "AsyncPreorderStoreBreadthFirstTreenumerator", "PreorderStoreBreadthFirstTreenumerator", "Copse.Treenumerators"),
      new("Copse.Async/Treenumerators/AsyncLevelOrderStoreDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/LevelOrderStoreDepthFirstTreenumerator.g.cs",
        "AsyncLevelOrderStoreDepthFirstTreenumerator", "LevelOrderStoreDepthFirstTreenumerator", "Copse.Treenumerators"),
      new("Copse.Async/Treenumerators/AsyncLevelOrderStoreBreadthFirstTreenumerator.cs",
        "Copse/Treenumerators/LevelOrderStoreBreadthFirstTreenumerator.g.cs",
        "AsyncLevelOrderStoreBreadthFirstTreenumerator", "LevelOrderStoreBreadthFirstTreenumerator", "Copse.Treenumerators"),

      new("Copse.Async/Treenumerators/AsyncBreadthFirstTreenumerator.cs",
        "Copse/Treenumerators/BreadthFirstTreenumerator.g.cs",
        "AsyncBreadthFirstTreenumerator", "BreadthFirstTreenumerator", "Copse.Treenumerators"),

      // The forward-only STREAM treenumerators: the twin takes over the canonical name (the
      // hand-tuned out-style stream treenumerators are retired; struct-return SPI proven at parity).
      new("Copse.Async/Treenumerators/AsyncPreorderStreamDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/PreorderStreamDepthFirstTreenumerator.g.cs",
        "AsyncPreorderStreamDepthFirstTreenumerator", "PreorderStreamDepthFirstTreenumerator", "Copse.Treenumerators"),

      new("Copse.Async/Treenumerators/AsyncLevelOrderStreamBreadthFirstTreenumerator.cs",
        "Copse/Treenumerators/LevelOrderStreamBreadthFirstTreenumerator.g.cs",
        "AsyncLevelOrderStreamBreadthFirstTreenumerator", "LevelOrderStreamBreadthFirstTreenumerator", "Copse.Treenumerators"),

      // The serializer's async I/O layer: the async scanner and the two async text streams are the
      // sources; their sync twins are the forward-only deserialize path (all target frameworks).
      new("Copse.SimpleSerializer/AsyncValueTokenStreamScanner.cs",
        "Copse.SimpleSerializer/Generated/ValueTokenStreamScanner.g.cs",
        "AsyncValueTokenStreamScanner", "ValueTokenStreamScanner", "Copse.SimpleSerializer"),

      new("Copse.SimpleSerializer/AsyncPreorderTextStream.cs",
        "Copse.SimpleSerializer/Generated/PreorderTextStream.g.cs",
        "AsyncPreorderTextStream", "PreorderTextStream", "Copse.SimpleSerializer"),

      new("Copse.SimpleSerializer/AsyncLevelOrderTextStream.cs",
        "Copse.SimpleSerializer/Generated/LevelOrderTextStream.g.cs",
        "AsyncLevelOrderTextStream", "LevelOrderTextStream", "Copse.SimpleSerializer"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncPruneAfterTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedPruneAfterTreenumerator.g.cs",
        "AsyncPruneAfterTreenumerator", "GeneratedPruneAfterTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncTakeNodesUntilTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedTakeNodesUntilTreenumerator.g.cs",
        "AsyncTakeNodesUntilTreenumerator", "GeneratedTakeNodesUntilTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanDepthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedRootfixScanDepthFirstTreenumerator.g.cs",
        "AsyncRootfixScanDepthFirstTreenumerator", "GeneratedRootfixScanDepthFirstTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanBreadthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedRootfixScanBreadthFirstTreenumerator.g.cs",
        "AsyncRootfixScanBreadthFirstTreenumerator", "GeneratedRootfixScanBreadthFirstTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/StructuralMerge/AsyncStructuralMergeDepthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedStructuralMergeDepthFirstTreenumerator.g.cs",
        "AsyncStructuralMergeDepthFirstTreenumerator", "GeneratedStructuralMergeDepthFirstTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/StructuralMerge/AsyncStructuralMergeBreadthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedStructuralMergeBreadthFirstTreenumerator.g.cs",
        "AsyncStructuralMergeBreadthFirstTreenumerator", "GeneratedStructuralMergeBreadthFirstTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncWhereDepthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedWhereDepthFirstTreenumerator.g.cs",
        "AsyncWhereDepthFirstTreenumerator", "GeneratedWhereDepthFirstTreenumerator", "Copse.Linq.Generated"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncWhereBreadthFirstTreenumerator.cs",
        "Copse.Linq/Generated/GeneratedWhereBreadthFirstTreenumerator.g.cs",
        "AsyncWhereBreadthFirstTreenumerator", "GeneratedWhereBreadthFirstTreenumerator", "Copse.Linq.Generated"),
    };
  }
}
