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
      new("Copse.Async/Treenumerables/AsyncDelegatingTreenumerable.cs",
        "Copse/Treenumerables/DelegatingTreenumerable.g.cs",
        "AsyncDelegatingTreenumerable", "DelegatingTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncDelegatingDepthFirstTreenumerable.cs",
        "Copse/Treenumerables/DelegatingDepthFirstTreenumerable.g.cs",
        "AsyncDelegatingDepthFirstTreenumerable", "DelegatingDepthFirstTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncDelegatingBreadthFirstTreenumerable.cs",
        "Copse/Treenumerables/DelegatingBreadthFirstTreenumerable.g.cs",
        "AsyncDelegatingBreadthFirstTreenumerable", "DelegatingBreadthFirstTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncPreorderStreamTreenumerable.cs",
        "Copse/Treenumerables/PreorderStreamTreenumerable.g.cs",
        "AsyncPreorderStreamTreenumerable", "PreorderStreamTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncLevelOrderStreamTreenumerable.cs",
        "Copse/Treenumerables/LevelOrderStreamTreenumerable.g.cs",
        "AsyncLevelOrderStreamTreenumerable", "LevelOrderStreamTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncEmptyTreenumerable.cs",
        "Copse/Treenumerables/EmptyTreenumerable.g.cs",
        "AsyncEmptyTreenumerable", "EmptyTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncPreorderTreenumerable.cs",
        "Copse/Treenumerables/PreorderTreenumerable.g.cs",
        "AsyncPreorderTreenumerable", "PreorderTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncLevelOrderTreenumerable.cs",
        "Copse/Treenumerables/LevelOrderTreenumerable.g.cs",
        "AsyncLevelOrderTreenumerable", "LevelOrderTreenumerable", "Copse.Treenumerables"),
      new("Copse.Async/Treenumerables/AsyncTreenumerable.cs",
        "Copse/Treenumerables/Treenumerable.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Treenumerables"),
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

      // The memoize cluster: twins take over the CANONICAL names (big-bang adoption like the
      // engines -- the hand-written sync memoize machinery is retired; MemoizeTests plus the
      // conformance matrix are the non-differential oracle). The cluster's classes reference
      // each other, so Generated*-style validation naming is not an option here.
      new("Copse.Linq.Async/Treenumerables/IAsyncTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/ITreenumerableBuffer.g.cs",
        "IAsyncTreenumerableBuffer", "ITreenumerableBuffer", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerators/Memoize/AsyncMemoizeDepthFirstBuffer.cs",
        "Copse.Linq/Treenumerators/Memoize/MemoizeDepthFirstBuffer.g.cs",
        "AsyncMemoizeDepthFirstBuffer", "MemoizeDepthFirstBuffer", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Memoize/AsyncMemoizeBreadthFirstBuffer.cs",
        "Copse.Linq/Treenumerators/Memoize/MemoizeBreadthFirstBuffer.g.cs",
        "AsyncMemoizeBreadthFirstBuffer", "MemoizeBreadthFirstBuffer", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Memoize/AsyncMemoizeDepthFirstStore.cs",
        "Copse.Linq/Treenumerators/Memoize/MemoizeDepthFirstStore.g.cs",
        "AsyncMemoizeDepthFirstStore", "MemoizeDepthFirstStore", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Memoize/AsyncMemoizeBreadthFirstStore.cs",
        "Copse.Linq/Treenumerators/Memoize/MemoizeBreadthFirstStore.g.cs",
        "AsyncMemoizeBreadthFirstStore", "MemoizeBreadthFirstStore", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerables/AsyncMemoizeTreenumerable.cs",
        "Copse.Linq/Treenumerables/MemoizeTreenumerable.g.cs",
        "AsyncMemoizeTreenumerable", "MemoizeTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/AsyncMemoizeDepthFirstSourceTreenumerable.cs",
        "Copse.Linq/Treenumerables/MemoizeDepthFirstSourceTreenumerable.g.cs",
        "AsyncMemoizeDepthFirstSourceTreenumerable", "MemoizeDepthFirstSourceTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/AsyncMemoizeBreadthFirstSourceTreenumerable.cs",
        "Copse.Linq/Treenumerables/MemoizeBreadthFirstSourceTreenumerable.g.cs",
        "AsyncMemoizeBreadthFirstSourceTreenumerable", "MemoizeBreadthFirstSourceTreenumerable", "Copse.Linq.Treenumerables"),

      // The capture-op plumbing (LeaffixScan/Invert): canonical-name adoption, same as the
      // memoize cluster.
      new("Copse.Linq.Async/Treenumerables/AsyncCompletedTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/CompletedTreenumerableBuffer.g.cs",
        "AsyncCompletedTreenumerableBuffer", "CompletedTreenumerableBuffer", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerators/Invert/AsyncInvertedLevelOrderStream.cs",
        "Copse.Linq/Treenumerators/Invert/InvertedLevelOrderStream.g.cs",
        "AsyncInvertedLevelOrderStream", "InvertedLevelOrderStream", "Copse.Linq.Treenumerators"),
    };
  }
}
