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

      // The tree-source factories: AsyncTree is the source of truth for Tree (the async-acquire
      // Using overloads are async-only marker regions -- their transcription would collapse onto
      // the sync-acquire twins). Retires the last hand-written concrete-treenumerable exception.
      new("Copse.Async/Treenumerables/AsyncTree.cs",
        "Copse/Treenumerables/Tree.g.cs",
        "AsyncTree", "Tree", "Copse.Treenumerables"),

      new("Copse.Async/Treenumerators/AsyncDisposeActionTreenumerator.cs",
        "Copse/Treenumerators/DisposeActionTreenumerator.g.cs",
        "AsyncDisposeActionTreenumerator", "DisposeActionTreenumerator", "Copse.Treenumerators"),
      new("Copse.Async/Treenumerators/AsyncDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/DepthFirstTreenumerator.g.cs",
        "AsyncDepthFirstTreenumerator", "DepthFirstTreenumerator", "Copse.Treenumerators"),
      // ChildResult: the child-enumerator protocol's read struct, per-color beside its
      // contract (IChildEnumerator / IAsyncChildEnumerator), identity-named like the reads.
      new("Copse.Async/ChildResult.cs",
        "Copse/ChildResult.g.cs",
        "ChildResult", "ChildResult", "Copse"),

      // The store SPIs, reads, and completed array stores: each color owns its own (decided
      // 2026-07-14 -- the de-share; Primitives/FlatStores retired). Async is the source.
      new("Copse.Async/Stores/IAsyncPreorderStore.cs",
        "Copse/Stores/IPreorderStore.g.cs",
        "IAsyncPreorderStore", "IPreorderStore", "Copse.Stores"),
      new("Copse.Async/Stores/IAsyncLevelOrderStore.cs",
        "Copse/Stores/ILevelOrderStore.g.cs",
        "IAsyncLevelOrderStore", "ILevelOrderStore", "Copse.Stores"),
      new("Copse.Async/Stores/IAsyncPreorderStream.cs",
        "Copse/Stores/IPreorderStream.g.cs",
        "IAsyncPreorderStream", "IPreorderStream", "Copse.Stores"),
      new("Copse.Async/Stores/IAsyncLevelOrderStream.cs",
        "Copse/Stores/ILevelOrderStream.g.cs",
        "IAsyncLevelOrderStream", "ILevelOrderStream", "Copse.Stores"),
      new("Copse.Async/Stores/PreorderRead.cs",
        "Copse/Stores/PreorderRead.g.cs",
        "PreorderRead", "PreorderRead", "Copse.Stores"),
      new("Copse.Async/Stores/LevelOrderRead.cs",
        "Copse/Stores/LevelOrderRead.g.cs",
        "LevelOrderRead", "LevelOrderRead", "Copse.Stores"),
      new("Copse.Async/Stores/AsyncPreorderArrayStore.cs",
        "Copse/Stores/PreorderArrayStore.g.cs",
        "AsyncPreorderArrayStore", "PreorderArrayStore", "Copse.Stores"),
      new("Copse.Async/Stores/AsyncLevelOrderArrayStore.cs",
        "Copse/Stores/LevelOrderArrayStore.g.cs",
        "AsyncLevelOrderArrayStore", "LevelOrderArrayStore", "Copse.Stores"),

      new("Copse.Async/Stores/AsyncPreorderCapture.cs",
        "Copse/Stores/PreorderCapture.g.cs",
        "AsyncPreorderCapture", "PreorderCapture", "Copse.Stores"),
      new("Copse.Async/Stores/AsyncLevelOrderCapture.cs",
        "Copse/Stores/LevelOrderCapture.g.cs",
        "AsyncLevelOrderCapture", "LevelOrderCapture", "Copse.Stores"),
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

      // The serializer's WRITE side: block-buffered async writers are the sources; their sync
      // twins (and the sync Serialize fluent surface) are generated.
      new("Copse.SimpleSerializer/AsyncPreorderTextWriter.cs",
        "Copse.SimpleSerializer/Generated/PreorderTextWriter.g.cs",
        "AsyncPreorderTextWriter", "PreorderTextWriter", "Copse.SimpleSerializer"),

      new("Copse.SimpleSerializer/AsyncLevelOrderTextWriter.cs",
        "Copse.SimpleSerializer/Generated/LevelOrderTextWriter.g.cs",
        "AsyncLevelOrderTextWriter", "LevelOrderTextWriter", "Copse.SimpleSerializer"),

      new("Copse.SimpleSerializer/TreeSerializer.SerializeAsync.cs",
        "Copse.SimpleSerializer/Generated/TreeSerializer.Serialize.g.cs",
        "TreeSerializer", "TreeSerializer", "Copse.SimpleSerializer"),

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

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncIdentitySelector.cs",
        "Copse.Linq/Treenumerators/Filter/IdentitySelector.g.cs",
        "AsyncIdentitySelector", "IdentitySelector", "Copse.Linq.Treenumerators"),

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
      new("Copse.Linq.Async/Treenumerables/IAsyncMemoizeTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/IMemoizeTreenumerableBuffer.g.cs",
        "IAsyncMemoizeTreenumerableBuffer", "IMemoizeTreenumerableBuffer", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Stores/Memoize/AsyncMemoizePreorderStore.cs",
        "Copse.Linq/Stores/Memoize/MemoizePreorderStore.g.cs",
        "AsyncMemoizePreorderStore", "MemoizePreorderStore", "Copse.Linq.Stores"),

      new("Copse.Linq.Async/Stores/Memoize/AsyncMemoizeLevelOrderStore.cs",
        "Copse.Linq/Stores/Memoize/MemoizeLevelOrderStore.g.cs",
        "AsyncMemoizeLevelOrderStore", "MemoizeLevelOrderStore", "Copse.Linq.Stores"),

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
      new("Copse.Linq.Async/Treenumerables/AsyncTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/TreenumerableBuffer.g.cs",
        "AsyncTreenumerableBuffer", "TreenumerableBuffer", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerators/Invert/AsyncInvertedLevelOrderStream.cs",
        "Copse.Linq/Treenumerators/Invert/InvertedLevelOrderStream.g.cs",
        "AsyncInvertedLevelOrderStream", "InvertedLevelOrderStream", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Stores/AsyncLazyPreorderStore.cs",
        "Copse.Linq/Stores/LazyPreorderStore.g.cs",
        "AsyncLazyPreorderStore", "LazyPreorderStore", "Copse.Linq.Stores"),

      new("Copse.Linq.Async/Stores/AsyncLazyLevelOrderStore.cs",
        "Copse.Linq/Stores/LazyLevelOrderStore.g.cs",
        "AsyncLazyLevelOrderStore", "LazyLevelOrderStore", "Copse.Linq.Stores"),

      // The fluent-layer construction machinery (Copse.Linq is generated from Copse.Linq.Async).
      new("Copse.Linq.Async/AsyncTreenumerableFactory.cs",
        "Copse.Linq/TreenumerableFactory.g.cs",
        "AsyncTreenumerableFactory", "TreenumerableFactory", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/AsyncHideTreenumerable.cs",
        "Copse.Linq/Treenumerables/HideTreenumerable.g.cs",
        "AsyncHideTreenumerable", "HideTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/IAsyncSelectTreenumerable.cs",
        "Copse.Linq/Treenumerables/ISelectTreenumerable.g.cs",
        "IAsyncSelectTreenumerable", "ISelectTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/AsyncSelectTreenumerable.cs",
        "Copse.Linq/Treenumerables/SelectTreenumerable.g.cs",
        "AsyncSelectTreenumerable", "SelectTreenumerable", "Copse.Linq.Treenumerables"),

      // The fluent extension layer: every Treenumerable.X partial is generated from its
      // AsyncTreenumerable.X source (ToList is async-only; the empty partial base has no twin).
      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.AllNodes.cs",
        "Copse.Linq/Treenumerable/Treenumerable.AllNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.AnyNodes.cs",
        "Copse.Linq/Treenumerable/Treenumerable.AnyNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Consume.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Consume.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.CountNodes.cs",
        "Copse.Linq/Treenumerable/Treenumerable.CountNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.CountTrees.cs",
        "Copse.Linq/Treenumerable/Treenumerable.CountTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Do.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Do.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.GetBranches.cs",
        "Copse.Linq/Treenumerable/Treenumerable.GetBranches.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.GetLeaves.cs",
        "Copse.Linq/Treenumerable/Treenumerable.GetLeaves.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.GetLevels.cs",
        "Copse.Linq/Treenumerable/Treenumerable.GetLevels.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.GetRoots.cs",
        "Copse.Linq/Treenumerable/Treenumerable.GetRoots.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.GetTraversals.cs",
        "Copse.Linq/Treenumerable/Treenumerable.GetTraversals.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.GetTreenumerator.cs",
        "Copse.Linq/Treenumerable/Treenumerable.GetTreenumerator.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Hide.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Hide.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Intersection.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Intersection.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Invert.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Invert.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.LeaffixAggregate.cs",
        "Copse.Linq/Treenumerable/Treenumerable.LeaffixAggregate.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.LeaffixScan.cs",
        "Copse.Linq/Treenumerable/Treenumerable.LeaffixScan.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.LevelOrderTraversal.cs",
        "Copse.Linq/Treenumerable/Treenumerable.LevelOrderTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Materialize.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Materialize.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Memoize.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Memoize.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.OrderChildrenBy.cs",
        "Copse.Linq/Treenumerable/Treenumerable.OrderChildrenBy.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.PostorderTraversal.cs",
        "Copse.Linq/Treenumerable/Treenumerable.PostorderTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.PreorderTraversal.cs",
        "Copse.Linq/Treenumerable/Treenumerable.PreorderTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.PruneAfter.cs",
        "Copse.Linq/Treenumerable/Treenumerable.PruneAfter.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.PruneBefore.cs",
        "Copse.Linq/Treenumerable/Treenumerable.PruneBefore.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.RootfixAggregate.cs",
        "Copse.Linq/Treenumerable/Treenumerable.RootfixAggregate.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.RootfixScan.cs",
        "Copse.Linq/Treenumerable/Treenumerable.RootfixScan.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Select.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Select.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.SkipLastTrees.cs",
        "Copse.Linq/Treenumerable/Treenumerable.SkipLastTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.SkipTrees.cs",
        "Copse.Linq/Treenumerable/Treenumerable.SkipTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Subtract.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Subtract.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.SymmetricDifference.cs",
        "Copse.Linq/Treenumerable/Treenumerable.SymmetricDifference.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.TakeLastTrees.cs",
        "Copse.Linq/Treenumerable/Treenumerable.TakeLastTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.TakeNodesUntil.cs",
        "Copse.Linq/Treenumerable/Treenumerable.TakeNodesUntil.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.TakeNodesWhile.cs",
        "Copse.Linq/Treenumerable/Treenumerable.TakeNodesWhile.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.TakeTrees.cs",
        "Copse.Linq/Treenumerable/Treenumerable.TakeTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),



      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.ToFormattedLines.cs",
        "Copse.Linq/Treenumerable/Treenumerable.ToFormattedLines.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.ToFormattedString.cs",
        "Copse.Linq/Treenumerable/Treenumerable.ToFormattedString.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Union.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Union.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerable/AsyncTreenumerable.Where.cs",
        "Copse.Linq/Treenumerable/Treenumerable.Where.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      // The tree-tokenizer machinery (the last hand-written Copse.Linq cluster): the async

    };
  }
}
