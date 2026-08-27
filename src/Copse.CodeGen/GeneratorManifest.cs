namespace Copse.CodeGen
{
  /// <summary>One async source and the sync twin it transcribes into (paths relative to the <c>src</c> root).</summary>
  public readonly record struct GeneratorEntry(
    string AsyncSource,
    string Twin,
    string AsyncClass,
    string SyncClass,
    string SyncNamespace);

  /// <summary>The traversal dimension a narrow twin keeps.</summary>
  public enum NarrowDimension { DepthFirst, BreadthFirst }

  /// <summary>
  /// One composite-width async source and the narrow (single-dimension) async twin it transcribes
  /// into (paths relative to the <c>src</c> root). The twin is itself an async source: the sync
  /// manifest lists the generated file as its input, so the narrow phase runs first (Program) and
  /// one composite-width file fans out to five generated ones.
  /// </summary>
  public readonly record struct NarrowGeneratorEntry(
    string WideSource,
    string Twin,
    NarrowDimension Dimension);

  /// <summary>
  /// The async-source -&gt; generated-sync-twin manifest. Single source of truth for both the regen
  /// tool (Program) and the drift-guard test. Each entry carries the target class name and namespace so
  /// a twin can take over the CANONICAL engine name (e.g. AsyncDepthFirstTreenumerator ->
  /// DepthFirstTreenumerator in Copse.Treenumerators) once the hand-tuned engine is retired, while other
  /// twins stay Generated* until their hand-tuned original is retired.
  /// </summary>
  public static class GeneratorManifest
  {
    // The SelectWhere lattice's narrow halves, generated per dimension from the composite-width
    // sources (see CompositeToNarrow). These run BEFORE the sync entries below, which list the
    // generated twins as inputs.
    public static readonly NarrowGeneratorEntry[] NarrowEntries =
    {
      new("Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereDepthFirstTreenumerable.g.cs",
        NarrowDimension.DepthFirst),
      new("Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereBreadthFirstTreenumerable.g.cs",
        NarrowDimension.BreadthFirst),


      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereDepthFirstTreenumerable.g.cs",
        NarrowDimension.DepthFirst),
      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereBreadthFirstTreenumerable.g.cs",
        NarrowDimension.BreadthFirst),

      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/Select/AsyncSelectDepthFirstTreenumerable.g.cs",
        NarrowDimension.DepthFirst),
      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/Select/AsyncSelectBreadthFirstTreenumerable.g.cs",
        NarrowDimension.BreadthFirst),

      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereDepthFirstTreenumerable.g.cs",
        NarrowDimension.DepthFirst),
      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereBreadthFirstTreenumerable.g.cs",
        NarrowDimension.BreadthFirst),

      new("Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereDepthFirstTreenumerable.g.cs",
        NarrowDimension.DepthFirst),
      new("Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereBreadthFirstTreenumerable.g.cs",
        NarrowDimension.BreadthFirst),
    };

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
      new("Copse.Async/Treenumerables/AsyncHierarchicalTreenumerable.cs",
        "Copse/Treenumerables/HierarchicalTreenumerable.g.cs",
        "AsyncHierarchicalTreenumerable", "HierarchicalTreenumerable", "Copse.Treenumerables"),

      // The tree-source factories: AsyncTree is the source of truth for Tree (the async-acquire
      // Using overloads are async-only marker regions -- their transcription would collapse onto
      // the sync-acquire twins). Retires the last hand-written concrete-treenumerable exception.
      new("Copse.Async/AsyncTree.cs",
        "Copse/Tree.g.cs",
        "AsyncTree", "Tree", "Copse"),
      new("Copse.Async/ChildEnumerators/AsyncTopologyChildEnumerator.cs",
        "Copse/ChildEnumerators/TopologyChildEnumerator.g.cs",
        "AsyncTopologyChildEnumerator", "TopologyChildEnumerator", "Copse.ChildEnumerators"),

      new("Copse.Async/Treenumerators/AsyncDisposeActionTreenumerator.cs",
        "Copse/Treenumerators/DisposeActionTreenumerator.g.cs",
        "AsyncDisposeActionTreenumerator", "DisposeActionTreenumerator", "Copse.Treenumerators"),
      new("Copse.Async/Treenumerators/AsyncHierarchicalDepthFirstTreenumerator.cs",
        "Copse/Treenumerators/HierarchicalDepthFirstTreenumerator.g.cs",
        "AsyncHierarchicalDepthFirstTreenumerator", "HierarchicalDepthFirstTreenumerator", "Copse.Treenumerators"),
      // The walker core (WALKABLE_CONTRACT_DESIGN.md §8b): the comonad's carrier, its result
      // struct, the Walk adapter, the extend/severed-view machinery, and the extension
      // surface -- authored async, generated into the base Copse project (the walker ships
      // with the engine and factories, not the operators; the lens family and the topology
      // factory it composes with stay Linq).
      new("Copse.Core.Async/AsyncTreeWalker.cs",
        "Copse.Core/TreeWalker.g.cs",
        "AsyncTreeWalker", "TreeWalker", "Copse.Core"),
      new("Copse.Core.Async/AsyncTreeWalkerResult.cs",
        "Copse.Core/TreeWalkerResult.g.cs",
        "AsyncTreeWalkerResult", "TreeWalkerResult", "Copse.Core"),
      new("Copse.Linq.Async/Treenumerables/Walkable/AsyncExtendWalkable.cs",
        "Copse.Linq/Treenumerables/Walkable/ExtendWalkable.g.cs",
        "AsyncExtendWalkable", "ExtendWalkable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Topologies/AsyncLazyTopology.cs",
        "Copse.Linq/Topologies/LazyTopology.g.cs",
        "AsyncLazyTopology", "LazyTopology", "Copse.Linq.Topologies"),
      new("Copse.Linq.Async/Topologies/AsyncTreeTopology.cs",
        "Copse.Linq/Topologies/TreeTopology.g.cs",
        "AsyncTreeTopology", "TreeTopology", "Copse.Linq"),
      new("Copse.Linq.Async/Treenumerables/Walkable/AsyncSubtreeWalkable.cs",
        "Copse.Linq/Treenumerables/Walkable/SubtreeWalkable.g.cs",
        "AsyncSubtreeWalkable", "SubtreeWalkable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/Walkable/AsyncTopologyWalkable.cs",
        "Copse.Linq/Treenumerables/Walkable/TopologyWalkable.g.cs",
        "AsyncTopologyWalkable", "TopologyWalkable", "Copse.Linq.Treenumerables"),
      // The lens family + SpanningSubtree crossed colors 2026-08-14 (they were the walker
      // arc's last sync-only hand files, authored during the capstone review before the
      // section-8 crossing; the async sources are now the edit surface).
      new("Copse.Linq.Async/Treenumerables/Walkable/AsyncPruneDescendantsWhereWalkable.cs",
        "Copse.Linq/Treenumerables/Walkable/PruneDescendantsWhereWalkable.g.cs",
        "AsyncPruneDescendantsWhereWalkable", "PruneDescendantsWhereWalkable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.PruneDescendantsWhere.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.PruneDescendantsWhere.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.SpanningSubtree.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.SpanningSubtree.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.Extend.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.Extend.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.Subtrees.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.Subtrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.GetHandles.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.GetHandles.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.GetHandlesWithNodes.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.GetHandlesWithNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.GetTreeWalkerAt.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.GetTreeWalkerAt.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/WalkableTreenumerable/AsyncTreenumerable.TryGetTreeWalkerAtRootIndex.cs",
        "Copse.Linq/TreenumerableExtensions/WalkableTreenumerable/Treenumerable.TryGetTreeWalkerAtRootIndex.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      // The walker-receiver extensions (the comonad's algebra) live in their receiver's
      // subfolder, one operator per file, same one-class partial as everything else.
      new("Copse.Linq.Async/TreenumerableExtensions/TreeWalker/AsyncTreeWalker.Extend.cs",
        "Copse.Linq/TreenumerableExtensions/TreeWalker/TreeWalker.Extend.g.cs",
        "AsyncTreeWalker", "TreeWalker", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/TreeWalker/AsyncTreeWalker.Duplicate.cs",
        "Copse.Linq/TreenumerableExtensions/TreeWalker/TreeWalker.Duplicate.g.cs",
        "AsyncTreeWalker", "TreeWalker", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/TreeWalker/AsyncTreeWalker.Subtree.cs",
        "Copse.Linq/TreenumerableExtensions/TreeWalker/TreeWalker.Subtree.g.cs",
        "AsyncTreeWalker", "TreeWalker", "Copse.Linq"),
      // FindHandles/FindHandle and HandleResult retired 2026-08-14 under the search law
      // (naming grammar): searches are not surface -- GetHandles/GetHandlesWithNodes plus
      // consumer LINQ express them, and a search's honest miss is the empty sequence.

      // The walkable contract family (design-docs/WALKABLE_CONTRACT_DESIGN.md step 1): the adjacency
      // contract and its upward result struct cross colors -- async is the source, the walker
      // tier's sync PoC files demote to twins.
      new("Copse.Core.Async/IAsyncWalkableTreenumerable.cs",
        "Copse.Core/IWalkableTreenumerable.g.cs",
        "IAsyncWalkableTreenumerable", "IWalkableTreenumerable", "Copse.Core"),

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
      new("Copse.Async/Stores/AsyncPreorderRead.cs",
        "Copse/Stores/PreorderRead.g.cs",
        "AsyncPreorderRead", "PreorderRead", "Copse.Stores"),
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

      new("Copse.Async/Treenumerators/AsyncHierarchicalBreadthFirstTreenumerator.cs",
        "Copse/Treenumerators/HierarchicalBreadthFirstTreenumerator.g.cs",
        "AsyncHierarchicalBreadthFirstTreenumerator", "HierarchicalBreadthFirstTreenumerator", "Copse.Treenumerators"),

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
      new("Copse.Linq.Async/Treenumerators/Filter/AsyncPruneDescendantsWhereTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/PruneDescendantsWhereTreenumerator.g.cs",
        "AsyncPruneDescendantsWhereTreenumerator", "PruneDescendantsWhereTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncTakeNodesUntilTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/TakeNodesUntilTreenumerator.g.cs",
        "AsyncTakeNodesUntilTreenumerator", "TakeNodesUntilTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncTakeSubtreesWhereTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/TakeSubtreesWhereTreenumerator.g.cs",
        "AsyncTakeSubtreesWhereTreenumerator", "TakeSubtreesWhereTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/RootfixScan/RootfixScanDepthFirstTreenumerator.g.cs",
        "AsyncRootfixScanDepthFirstTreenumerator", "RootfixScanDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/RootfixScan/RootfixScanBreadthFirstTreenumerator.g.cs",
        "AsyncRootfixScanBreadthFirstTreenumerator", "RootfixScanBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      // The streaming projection citizenship's product twins + citizen wrappers
      // (SELECT_INTO_CAPTURES_DESIGN.md): the plain engines above stay untouched.
      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanProductDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/RootfixScan/RootfixScanProductDepthFirstTreenumerator.g.cs",
        "AsyncRootfixScanProductDepthFirstTreenumerator", "RootfixScanProductDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/RootfixScan/AsyncRootfixScanProductBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/RootfixScan/RootfixScanProductBreadthFirstTreenumerator.g.cs",
        "AsyncRootfixScanProductBreadthFirstTreenumerator", "RootfixScanProductBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerables/AsyncRootfixScanTreenumerable.cs",
        "Copse.Linq/Treenumerables/RootfixScanTreenumerable.g.cs",
        "AsyncRootfixScanTreenumerable", "RootfixScanTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/AsyncRootfixScanProductTreenumerable.cs",
        "Copse.Linq/Treenumerables/RootfixScanProductTreenumerable.g.cs",
        "AsyncRootfixScanProductTreenumerable", "RootfixScanProductTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerators/StructuralMerge/AsyncStructuralMergeDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/StructuralMerge/StructuralMergeDepthFirstTreenumerator.g.cs",
        "AsyncStructuralMergeDepthFirstTreenumerator", "StructuralMergeDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerators/StructuralMerge/AsyncStructuralMergeBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/StructuralMerge/StructuralMergeBreadthFirstTreenumerator.g.cs",
        "AsyncStructuralMergeBreadthFirstTreenumerator", "StructuralMergeBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/AsyncExpansion.cs",
        "Copse.Linq/Expansion.g.cs",
        "AsyncExpansion", "Expansion", "Copse.Linq"),
      new("Copse.Linq.Async/Treenumerators/SelectMany/AsyncSelectManyDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/SelectMany/SelectManyDepthFirstTreenumerator.g.cs",
        "AsyncSelectManyDepthFirstTreenumerator", "SelectManyDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),
      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.SelectMany.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.SelectMany.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
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
      // The public projection citizenship (SELECT_INTO_CAPTURES_DESIGN.md): per-tier
      // public contracts; closure is enforced by the ComposeSelect return types.
      new("Copse.Linq.Async/Treenumerables/IAsyncSelectTreenumerable.cs",
        "Copse.Linq/Treenumerables/ISelectTreenumerable.g.cs",
        "IAsyncSelectTreenumerable", "ISelectTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/IAsyncPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/IPruneDescendantsWhereTreenumerable.g.cs",
        "IAsyncPruneDescendantsWhereTreenumerable", "IPruneDescendantsWhereTreenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/Treenumerables/Buffers/IAsyncSelectTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/Buffers/ISelectTreenumerableBuffer.g.cs",
        "IAsyncSelectTreenumerableBuffer", "ISelectTreenumerableBuffer", "Copse.Linq"),
      new("Copse.Linq.Async/Treenumerables/Select/IAsyncProjectionSource.cs",
        "Copse.Linq/Treenumerables/Select/IProjectionSource.g.cs",
        "IAsyncProjectionSource", "IProjectionSource", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectTreenumerable.CaptureThrough.cs",
        "Copse.Linq/Treenumerables/Select/SelectTreenumerable.CaptureThrough.g.cs",
        "AsyncSelectTreenumerable", "SelectTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectTreenumerable.Citizenship.cs",
        "Copse.Linq/Treenumerables/Select/SelectTreenumerable.Citizenship.g.cs",
        "AsyncSelectTreenumerable", "SelectTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereTreenumerable.Citizenship.cs",
        "Copse.Linq/Treenumerables/PruneDescendantsWhere/PruneDescendantsWhereTreenumerable.Citizenship.g.cs",
        "AsyncPruneDescendantsWhereTreenumerable", "PruneDescendantsWhereTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereTreenumerable.Citizenship.cs",
        "Copse.Linq/Treenumerables/SelectPruneDescendantsWhere/SelectPruneDescendantsWhereTreenumerable.Citizenship.g.cs",
        "AsyncSelectPruneDescendantsWhereTreenumerable", "SelectPruneDescendantsWhereTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereTreenumerable.Citizenship.cs",
        "Copse.Linq/Treenumerables/SelectWhere/SelectWhereTreenumerable.Citizenship.g.cs",
        "AsyncSelectWhereTreenumerable", "SelectWhereTreenumerable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/Buffers/AsyncProjectedTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/Buffers/ProjectedTreenumerableBuffer.g.cs",
        "AsyncProjectedTreenumerableBuffer", "ProjectedTreenumerableBuffer", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/AsyncTakeSubtreesWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/TakeSubtreesWhereTreenumerable.g.cs",
        "AsyncTakeSubtreesWhereTreenumerable", "TakeSubtreesWhereTreenumerable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/AsyncTakeSubtreesWhereProductTreenumerable.cs",
        "Copse.Linq/Treenumerables/TakeSubtreesWhereProductTreenumerable.g.cs",
        "AsyncTakeSubtreesWhereProductTreenumerable", "TakeSubtreesWhereProductTreenumerable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/Buffers/IAsyncTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/Buffers/ITreenumerableBuffer.g.cs",
        "IAsyncTreenumerableBuffer", "ITreenumerableBuffer", "Copse.Linq"),
      new("Copse.Linq.Async/Treenumerables/Buffers/IAsyncMemoizeTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/Buffers/IMemoizeTreenumerableBuffer.g.cs",
        "IAsyncMemoizeTreenumerableBuffer", "IMemoizeTreenumerableBuffer", "Copse.Linq"),
      // The adjacency engines (the buffer re-parent, WALKABLE_CONTRACT_DESIGN.md step 2),
      // two citizens per layout since the 2026-08-16 split: the *AdjacencyIndex scans serve
      // GROWING stores incrementally; the *ArrayTopology engines serve COMPLETED array
      // stores via span arithmetic plus a one-shot parent map.
      new("Copse.Core.Async/IAsyncTreeTopology.cs",
        "Copse.Core/ITreeTopology.g.cs",
        "IAsyncTreeTopology", "ITreeTopology", "Copse.Core"),
      new("Copse.Linq.Async/Topologies/AsyncPreorderAdjacencyIndex.cs",
        "Copse.Linq/Topologies/PreorderAdjacencyIndex.g.cs",
        "AsyncPreorderAdjacencyIndex", "PreorderAdjacencyIndex", "Copse.Linq.Topologies"),
      new("Copse.Linq.Async/Topologies/AsyncLevelOrderAdjacencyIndex.cs",
        "Copse.Linq/Topologies/LevelOrderAdjacencyIndex.g.cs",
        "AsyncLevelOrderAdjacencyIndex", "LevelOrderAdjacencyIndex", "Copse.Linq.Topologies"),
      new("Copse.Linq.Async/Topologies/AsyncPreorderArrayTopology.cs",
        "Copse.Linq/Topologies/PreorderArrayTopology.g.cs",
        "AsyncPreorderArrayTopology", "PreorderArrayTopology", "Copse.Linq.Topologies"),
      new("Copse.Linq.Async/Topologies/AsyncLevelOrderArrayTopology.cs",
        "Copse.Linq/Topologies/LevelOrderArrayTopology.g.cs",
        "AsyncLevelOrderArrayTopology", "LevelOrderArrayTopology", "Copse.Linq.Topologies"),

      new("Copse.Linq.Async/Stores/Memoize/AsyncMemoizePreorderCapture.cs",
        "Copse.Linq/Stores/Memoize/MemoizePreorderCapture.g.cs",
        "AsyncMemoizePreorderCapture", "MemoizePreorderCapture", "Copse.Linq.Stores"),

      new("Copse.Linq.Async/Stores/Memoize/AsyncMemoizeLevelOrderCapture.cs",
        "Copse.Linq/Stores/Memoize/MemoizeLevelOrderCapture.g.cs",
        "AsyncMemoizeLevelOrderCapture", "MemoizeLevelOrderCapture", "Copse.Linq.Stores"),

      new("Copse.Linq.Async/Treenumerables/Buffers/AsyncMemoizeTreenumerable.cs",
        "Copse.Linq/Treenumerables/Buffers/MemoizeTreenumerable.g.cs",
        "AsyncMemoizeTreenumerable", "MemoizeTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/Buffers/AsyncMemoizeDepthFirstSourceTreenumerable.cs",
        "Copse.Linq/Treenumerables/Buffers/MemoizeDepthFirstSourceTreenumerable.g.cs",
        "AsyncMemoizeDepthFirstSourceTreenumerable", "MemoizeDepthFirstSourceTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/Buffers/AsyncMemoizeBreadthFirstSourceTreenumerable.cs",
        "Copse.Linq/Treenumerables/Buffers/MemoizeBreadthFirstSourceTreenumerable.g.cs",
        "AsyncMemoizeBreadthFirstSourceTreenumerable", "MemoizeBreadthFirstSourceTreenumerable", "Copse.Linq.Treenumerables"),

      // The lazy-Materialize settle pair (2026-08-10): the memo-completing buffer and its
      // first-pull settle treenumerator.
      new("Copse.Linq.Async/Treenumerables/Buffers/AsyncMaterializeTreenumerable.cs",
        "Copse.Linq/Treenumerables/Buffers/MaterializeTreenumerable.g.cs",
        "AsyncMaterializeTreenumerable", "MaterializeTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerators/Materialize/AsyncMaterializeTreenumerator.cs",
        "Copse.Linq/Treenumerators/Materialize/MaterializeTreenumerator.g.cs",
        "AsyncMaterializeTreenumerator", "MaterializeTreenumerator", "Copse.Linq.Treenumerators"),

      // The capture-op plumbing (LeaffixScan/Invert): canonical-name adoption, same as the
      // memoize cluster.
      new("Copse.Linq.Async/Treenumerables/Buffers/AsyncTreenumerableBuffer.cs",
        "Copse.Linq/Treenumerables/Buffers/TreenumerableBuffer.g.cs",
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

      new("Copse.Linq.Async/Treenumerables/AsyncHideTreenumerable.cs",
        "Copse.Linq/Treenumerables/HideTreenumerable.g.cs",
        "AsyncHideTreenumerable", "HideTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ISelectWhereTreenumerable.g.cs",
        "IAsyncSelectWhereTreenumerable", "ISelectWhereTreenumerable", "Copse.Linq.Treenumerables"),


      new("Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/SelectPruneDescendantsWhere/SelectPruneDescendantsWhereTreenumerable.g.cs",
        "AsyncSelectPruneDescendantsWhereTreenumerable", "SelectPruneDescendantsWhereTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerators/Filter/AsyncSelectPruneDescendantsWhereTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/SelectPruneDescendantsWhereTreenumerator.g.cs",
        "AsyncSelectPruneDescendantsWhereTreenumerator", "SelectPruneDescendantsWhereTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/IAsyncResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/IResultSelector.g.cs",
        "IAsyncResultSelector", "IResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/WhereResultSelector.g.cs",
        "AsyncWhereResultSelector", "WhereResultSelector", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncComposedResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/ComposedResultSelector.g.cs",
        "AsyncComposedResultSelector", "ComposedResultSelector", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectResultSelector.cs",
        "Copse.Linq/Treenumerables/Select/SelectResultSelector.g.cs",
        "AsyncSelectResultSelector", "SelectResultSelector", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/PruneDescendantsWhere/PruneDescendantsWhereResultSelector.g.cs",
        "AsyncPruneDescendantsWhereResultSelector", "PruneDescendantsWhereResultSelector", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncScanWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ScanWhereTreenumerable.g.cs",
        "AsyncScanWhereTreenumerable", "ScanWhereTreenumerable", "Copse.Linq.Treenumerables"),
      new("Copse.Linq.Async/Treenumerators/Filter/AsyncScanWhereDepthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/ScanWhereDepthFirstTreenumerator.g.cs",
        "AsyncScanWhereDepthFirstTreenumerator", "ScanWhereDepthFirstTreenumerator", "Copse.Linq.Treenumerators"),
      new("Copse.Linq.Async/Treenumerators/Filter/AsyncScanWhereBreadthFirstTreenumerator.cs",
        "Copse.Linq/Treenumerators/Filter/ScanWhereBreadthFirstTreenumerator.g.cs",
        "AsyncScanWhereBreadthFirstTreenumerator", "ScanWhereBreadthFirstTreenumerator", "Copse.Linq.Treenumerators"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncPositionalWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/PositionalWhereResultSelector.g.cs",
        "AsyncPositionalWhereResultSelector", "PositionalWhereResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncPruneSubtreesWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/PruneSubtreesWhereResultSelector.g.cs",
        "AsyncPruneSubtreesWhereResultSelector", "PruneSubtreesWhereResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncFuncResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/FuncResultSelector.g.cs",
        "AsyncFuncResultSelector", "FuncResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/PruneDescendantsWhere/PruneDescendantsWhereTreenumerable.g.cs",
        "AsyncPruneDescendantsWhereTreenumerable", "PruneDescendantsWhereTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncPositionalPruneSubtreesWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/PositionalPruneSubtreesWhereResultSelector.g.cs",
        "AsyncPositionalPruneSubtreesWhereResultSelector", "PositionalPruneSubtreesWhereResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncPruneSiblingsWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/PruneSiblingsWhereResultSelector.g.cs",
        "AsyncPruneSiblingsWhereResultSelector", "PruneSiblingsWhereResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncPositionalPruneSiblingsWhereResultSelector.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/PositionalPruneSiblingsWhereResultSelector.g.cs",
        "AsyncPositionalPruneSiblingsWhereResultSelector", "PositionalPruneSiblingsWhereResultSelector", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/ResultSelectors/AsyncSelectWhereResult.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ResultSelectors/SelectWhereResult.g.cs",
        "AsyncSelectWhereResult", "SelectWhereResult", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereTreenumerable.cs",
        "Copse.Linq/Treenumerables/SelectWhere/SelectWhereTreenumerable.g.cs",
        "AsyncSelectWhereTreenumerable", "SelectWhereTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectTreenumerable.cs",
        "Copse.Linq/Treenumerables/Select/SelectTreenumerable.g.cs",
        "AsyncSelectTreenumerable", "SelectTreenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereComposition.cs",
        "Copse.Linq/Treenumerables/SelectWhere/SelectWhereComposition.g.cs",
        "AsyncSelectWhereComposition", "SelectWhereComposition", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereDepthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ISelectWhereDepthFirstTreenumerable.g.cs",
        "IAsyncSelectWhereDepthFirstTreenumerable", "ISelectWhereDepthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/IAsyncSelectWhereBreadthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/SelectWhere/ISelectWhereBreadthFirstTreenumerable.g.cs",
        "IAsyncSelectWhereBreadthFirstTreenumerable", "ISelectWhereBreadthFirstTreenumerable", "Copse.Linq.Treenumerables"),


      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereDepthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/SelectWhere/SelectWhereDepthFirstTreenumerable.g.cs",
        "AsyncSelectWhereDepthFirstTreenumerable", "SelectWhereDepthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectWhere/AsyncSelectWhereBreadthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/SelectWhere/SelectWhereBreadthFirstTreenumerable.g.cs",
        "AsyncSelectWhereBreadthFirstTreenumerable", "SelectWhereBreadthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectDepthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/Select/SelectDepthFirstTreenumerable.g.cs",
        "AsyncSelectDepthFirstTreenumerable", "SelectDepthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/Select/AsyncSelectBreadthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/Select/SelectBreadthFirstTreenumerable.g.cs",
        "AsyncSelectBreadthFirstTreenumerable", "SelectBreadthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereDepthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/PruneDescendantsWhere/PruneDescendantsWhereDepthFirstTreenumerable.g.cs",
        "AsyncPruneDescendantsWhereDepthFirstTreenumerable", "PruneDescendantsWhereDepthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/PruneDescendantsWhere/AsyncPruneDescendantsWhereBreadthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/PruneDescendantsWhere/PruneDescendantsWhereBreadthFirstTreenumerable.g.cs",
        "AsyncPruneDescendantsWhereBreadthFirstTreenumerable", "PruneDescendantsWhereBreadthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereDepthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/SelectPruneDescendantsWhere/SelectPruneDescendantsWhereDepthFirstTreenumerable.g.cs",
        "AsyncSelectPruneDescendantsWhereDepthFirstTreenumerable", "SelectPruneDescendantsWhereDepthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      new("Copse.Linq.Async/Treenumerables/SelectPruneDescendantsWhere/AsyncSelectPruneDescendantsWhereBreadthFirstTreenumerable.g.cs",
        "Copse.Linq/Treenumerables/SelectPruneDescendantsWhere/SelectPruneDescendantsWhereBreadthFirstTreenumerable.g.cs",
        "AsyncSelectPruneDescendantsWhereBreadthFirstTreenumerable", "SelectPruneDescendantsWhereBreadthFirstTreenumerable", "Copse.Linq.Treenumerables"),

      // The fluent extension layer: every Treenumerable.X partial is generated from its
      // AsyncTreenumerable.X source (ToList is async-only; the empty partial base has no twin).
      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.AllNodes.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.AllNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.AnyNodes.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.AnyNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Consume.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Consume.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.CountNodes.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.CountNodes.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.CountTrees.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.CountTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Do.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Do.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetBranches.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetBranches.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetLeaves.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetLeaves.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetLevels.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetLevels.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetRoots.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetRoots.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetDepthFirstTraversal.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetDepthFirstTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetBreadthFirstTraversal.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetBreadthFirstTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),
      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetTraversal.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetTreenumerator.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetTreenumerator.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Hide.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Hide.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Intersection.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Intersection.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Invert.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Invert.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.LeaffixAggregate.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.LeaffixAggregate.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.LeaffixDispatch.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.LeaffixDispatch.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.LeaffixScan.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.LeaffixScan.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetLevelOrderTraversal.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetLevelOrderTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Materialize.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Materialize.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Memoize.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Memoize.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.OrderChildrenBy.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.OrderChildrenBy.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetPostorderTraversal.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetPostorderTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.GetPreorderTraversal.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.GetPreorderTraversal.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.PruneDescendantsWhere.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.PruneDescendantsWhere.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.PruneSiblingsWhere.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.PruneSiblingsWhere.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.PruneSubtreesWhere.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.PruneSubtreesWhere.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.RootfixAggregate.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.RootfixAggregate.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.RootfixDispatch.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.RootfixDispatch.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.RootfixScan.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.RootfixScan.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Select.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Select.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.SkipLastTrees.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.SkipLastTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.SkipTrees.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.SkipTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Subtract.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Subtract.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.SymmetricDifference.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.SymmetricDifference.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.TakeLastTrees.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.TakeLastTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.TakeNodesUntil.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.TakeNodesUntil.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.TakeNodesWhile.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.TakeNodesWhile.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.TakeSubtreesWhere.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.TakeSubtreesWhere.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.TakeTrees.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.TakeTrees.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),



      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.ToFormattedLines.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.ToFormattedLines.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.ToFormattedString.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.ToFormattedString.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Union.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Union.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      new("Copse.Linq.Async/TreenumerableExtensions/Treenumerable/AsyncTreenumerable.Where.cs",
        "Copse.Linq/TreenumerableExtensions/Treenumerable/Treenumerable.Where.g.cs",
        "AsyncTreenumerable", "Treenumerable", "Copse.Linq"),

      // The tree-tokenizer machinery (the last hand-written Copse.Linq cluster): the async

    };
  }
}
