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

      // The operator treenumerators: twins take over the CANONICAL names (the hand-tuned sync
      // operators are retired; the operators' own suites + VisitStreamConformance are the
      // non-differential oracle, so the Generated*-vs-hand-written differential tests go with
      // them -- same as the engine A/B teardown).
      new("Copse.Linq.Async/Treenumerators/Filter/AsyncPruneAfterTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/PruneAfterTreenumerator.g.cs",
        "AsyncPruneAfterTreenumerator", "PruneAfterTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncTakeNodesUntilTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/TakeNodesUntilTreenumerator.g.cs",
        "AsyncTakeNodesUntilTreenumerator", "TakeNodesUntilTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/RootfixScan/RootfixScanDepthFirstTreenumerator.g.cs",
        "AsyncRootfixScanDepthFirstTreenumerator", "RootfixScanDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/RootfixScan/RootfixScanBreadthFirstTreenumerator.g.cs",
        "AsyncRootfixScanBreadthFirstTreenumerator", "RootfixScanBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/StructuralMerge/AsyncStructuralMergeDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/StructuralMerge/StructuralMergeDepthFirstTreenumerator.g.cs",
        "AsyncStructuralMergeDepthFirstTreenumerator", "StructuralMergeDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/StructuralMerge/AsyncStructuralMergeBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/StructuralMerge/StructuralMergeBreadthFirstTreenumerator.g.cs",
        "AsyncStructuralMergeBreadthFirstTreenumerator", "StructuralMergeBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncWhereDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/WhereDepthFirstTreenumerator.g.cs",
        "AsyncWhereDepthFirstTreenumerator", "WhereDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncWhereBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/WhereBreadthFirstTreenumerator.g.cs",
        "AsyncWhereBreadthFirstTreenumerator", "WhereBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Do/AsyncDoTreenumerator.cs",
        "Copse.Linq/Treenumerators/Do/DoTreenumerator.g.cs",
        "AsyncDoTreenumerator", "DoTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Hide/AsyncHideTreenumerator.cs",
        "Copse.Linq/Treenumerators/Hide/HideTreenumerator.g.cs",
        "AsyncHideTreenumerator", "HideTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Select/AsyncSelectTreenumerator.cs",
        "Copse.Linq/Treenumerators/Select/SelectTreenumerator.g.cs",
        "AsyncSelectTreenumerator", "SelectTreenumerator", "Copse.Linq.Treenumerators"),

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
