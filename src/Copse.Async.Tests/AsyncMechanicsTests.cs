using Copse.Async.Stores;
using Copse;
using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.Linq.Async;
using Copse.Linq.Treenumerators;
using Copse.Treenumerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Validates the async EXECUTION MECHANICS -- suspend/resume, ValueTask, ref-not-across-await at
  // runtime -- NOT the traversal logic (that is proven once, on the generated sync twins, by
  // VisitStreamConformance). Each test drives an async treenumerator over a GENUINELY-SUSPENDING source
  // (Task.Yield on every pull) and asserts its stream equals the sync/generated-sync equivalent.
  [TestClass]
  public class AsyncMechanicsTests
  {
    //        1              6
    //      / | \            |
    //     2  3  4           7
    //        |
    //        5
    private static readonly Dictionary<int, int[]> Tree = new()
    {
      [1] = new[] { 2, 3, 4 },
      [3] = new[] { 5 },
      [6] = new[] { 7 },
    };
    private static readonly int[] Roots = { 1, 6 };

    private static int[] ChildrenOf(int node) => Tree.TryGetValue(node, out var c) ? c : Array.Empty<int>();

    private static readonly Func<NodeContext<int>, bool> KeepNot3 = nc => nc.Node != 3;
    private static readonly Func<int, bool> KeepNot3Value = n => n != 3;

    private static readonly Func<NodeContext<int>, Copse.Linq.Treenumerables.SelectWhereResult<int>> KeepNot3Result =
      nc => new Copse.Linq.Treenumerables.SelectWhereResult<int>(
        nc.Node,
        nc.Node != 3 ? NodeTraversalStrategies.TraverseAll : NodeTraversalStrategies.SkipNode);

    private static readonly Func<NodeContext<int>, Copse.Linq.Async.Treenumerables.SelectWhereResult<int>> AsyncKeepNot3Result =
      nc => new Copse.Linq.Async.Treenumerables.SelectWhereResult<int>(
        nc.Node,
        nc.Node != 3 ? NodeTraversalStrategies.TraverseAll : NodeTraversalStrategies.SkipNode);

    [TestMethod]
    public async Task AsyncDepthFirstEngine_MatchesSyncEngine()
    {
      var sync = Collect(new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
        Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n));

      var async = await CollectAsync(new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
        AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncBreadthFirstEngine_MatchesSyncEngine()
    {
      var sync = Collect(new BreadthFirstTreenumerator<int, int, SyncChildEnumerator>(
        Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n));

      var async = await CollectAsync(new AsyncBreadthFirstTreenumerator<int, int, AsyncChildEnumerator>(
        AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncWhereDepthFirst_OverSuspendingInner_MatchesGeneratedSyncWhere()
    {
      var sync = Collect(new WhereDepthFirstTreenumerator<int, int, Copse.Linq.Treenumerables.FuncResultSelector<int, int>>(
        () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        new Copse.Linq.Treenumerables.FuncResultSelector<int, int>(KeepNot3Result)));

      var async = await CollectAsync(new AsyncWhereDepthFirstTreenumerator<int, int, Copse.Linq.Async.Treenumerables.FuncResultSelector<int, int>>(
        () => new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        new Copse.Linq.Async.Treenumerables.FuncResultSelector<int, int>(AsyncKeepNot3Result)));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncWhereBreadthFirst_OverSuspendingBfsInner_MatchesGeneratedSyncWhere()
    {
      var sync = Collect(new WhereBreadthFirstTreenumerator<int, int, Copse.Linq.Treenumerables.FuncResultSelector<int, int>>(
        () => new BreadthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        new Copse.Linq.Treenumerables.FuncResultSelector<int, int>(KeepNot3Result)));

      var async = await CollectAsync(new AsyncWhereBreadthFirstTreenumerator<int, int, Copse.Linq.Async.Treenumerables.FuncResultSelector<int, int>>(
        () => new AsyncBreadthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        new Copse.Linq.Async.Treenumerables.FuncResultSelector<int, int>(AsyncKeepNot3Result)));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncPruneAfter_OverSuspendingInner_MatchesGeneratedSyncTwin()
    {
      Func<NodeContext<int>, bool> pruneAt3 = nc => nc.Node == 3;

      var sync = Collect(new PruneAfterTreenumerator<int>(
        () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        pruneAt3));

      var async = await CollectAsync(new AsyncPruneAfterTreenumerator<int>(
        () => new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        pruneAt3));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncTakeNodesUntil_OverSuspendingInner_MatchesGeneratedSyncTwin()
    {
      Func<NodeContext<int>, bool> stopAt3 = nc => nc.Node == 3;

      foreach (var keepFinalNode in new[] { true, false })
      {
        var sync = Collect(new TakeNodesUntilTreenumerator<int>(
          () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
            Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
          stopAt3, keepFinalNode));

        var async = await CollectAsync(new AsyncTakeNodesUntilTreenumerator<int>(
          () => new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
            AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
          stopAt3, keepFinalNode));

        CollectionAssert.AreEqual(sync, async, $"keepFinalNode={keepFinalNode}");
      }
    }

    [TestMethod]
    public async Task AsyncRootfixScan_OverSuspendingInner_MatchesGeneratedSyncTwins()
    {
      Func<NodeContext<int>, NodeContext<int>, int> sum = (acc, node) => acc.Node + node.Node;

      // Depth-first
      var syncDft = Collect(new RootfixScanDepthFirstTreenumerator<int, int>(
        () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        sum, 0));
      var asyncDft = await CollectAsync(new AsyncRootfixScanDepthFirstTreenumerator<int, int>(
        () => new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        sum, 0));
      CollectionAssert.AreEqual(syncDft, asyncDft, "DFT");

      // Breadth-first
      var syncBft = Collect(new RootfixScanBreadthFirstTreenumerator<int, int>(
        () => new BreadthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        sum, 0));
      var asyncBft = await CollectAsync(new AsyncRootfixScanBreadthFirstTreenumerator<int, int>(
        () => new AsyncBreadthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        sum, 0));
      CollectionAssert.AreEqual(syncBft, asyncBft, "BFT");
    }

    [TestMethod]
    public async Task AsyncStructuralMergeDepthFirst_OverSuspendingInners_MatchesGeneratedSyncTwin()
    {
      var sync = CollectMerge(new StructuralMergeDepthFirstTreenumerator<int, int>(
        () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n)));

      var async = await CollectMergeAsync(new AsyncStructuralMergeDepthFirstTreenumerator<int, int>(
        () => new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        () => new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n)));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncStructuralMergeBreadthFirst_OverSuspendingInners_MatchesGeneratedSyncTwin()
    {
      var sync = CollectMerge(new StructuralMergeBreadthFirstTreenumerator<int, int>(
        () => new BreadthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        () => new BreadthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n)));

      var async = await CollectMergeAsync(new AsyncStructuralMergeBreadthFirstTreenumerator<int, int>(
        () => new AsyncBreadthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        () => new AsyncBreadthFirstTreenumerator<int, int, AsyncChildEnumerator>(
          AsyncRoots(), nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n)));

      CollectionAssert.AreEqual(sync, async);
    }

    private static List<string> CollectMerge(ITreenumerator<MergeNode<int, int>> t)
    {
      var visits = new List<string>();
      using (t)
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          visits.Add(FormatMerge(t.Mode, t.Node, t.VisitCount, t.Position));
      return visits;
    }

    private static async Task<List<string>> CollectMergeAsync(IAsyncTreenumerator<MergeNode<int, int>> t)
    {
      var visits = new List<string>();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          visits.Add(FormatMerge(t.Mode, t.Node, t.VisitCount, t.Position));
      return visits;
    }

    private static string FormatMerge(TreenumeratorMode mode, MergeNode<int, int> node, int visitCount, NodePosition position)
      => $"{mode} L{node.HasLeft}:{node.Left} R{node.HasRight}:{node.Right} vc{visitCount} d{position.Depth} s{position.SiblingIndex}";

    [TestMethod]
    public async Task FluentWhereSelect_Composes()
    {
      // The deferred fluent operators compose on the async side: source.Where(...).Select(...).
      IAsyncTreenumerable<int> source = new AsyncTreenumerable<int, int, AsyncChildEnumerator>(
        nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n, AsyncRoots());

      var composed = await CollectAsync(source.Where(KeepNot3Value).Select(n => n * 10).GetAsyncDepthFirstTreenumerator());

      // Expected: the generated sync Where's first-visit nodes, mapped.
      var syncWhere = Collect(new WhereDepthFirstTreenumerator<int, int, Copse.Linq.Treenumerables.FuncResultSelector<int, int>>(
        () => new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
          Roots, nc => new SyncChildEnumerator(ChildrenOf(nc.Node)), n => n),
        new Copse.Linq.Treenumerables.FuncResultSelector<int, int>(KeepNot3Result)));

      var expected = FirstVisitNodes(syncWhere).Select(n => n * 10).ToList();
      var actual = FirstVisitNodes(composed);

      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task Terminals_CountAndToList_OverAsyncPipeline()
    {
      IAsyncTreenumerable<int> source = new AsyncTreenumerable<int, int, AsyncChildEnumerator>(
        nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n, AsyncRoots());

      // 7 nodes in the forest; DFT schedule order.
      Assert.AreEqual(7, await source.CountNodesAsync());
      CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 4, 6, 7 }, await source.ToListAsync());

      // Where(drop 3) promotes child 5, leaving 6 nodes.
      Assert.AreEqual(6, await source.Where(KeepNot3Value).CountNodesAsync());
      CollectionAssert.AreEqual(new[] { 1, 2, 5, 4, 6, 7 }, await source.Where(KeepNot3Value).ToListAsync());
    }

    [TestMethod]
    public async Task DoAndHide_ForwardStreamUnchanged_OverAsyncPipeline()
    {
      IAsyncTreenumerable<int> source = new AsyncTreenumerable<int, int, AsyncChildEnumerator>(
        nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n, AsyncRoots());

      var baseline = await source.ToListAsync();

      // Hide forwards the visit stream unchanged behind the plain contract.
      CollectionAssert.AreEqual(baseline, await source.Hide().ToListAsync());

      // Do forwards unchanged AND runs its side effect on every emitted visit (schedule + visiting),
      // so it observes strictly more than the schedule-only node list.
      var seen = new List<int>();
      var withDo = await source.Do(v => seen.Add(v.Node)).ToListAsync();
      CollectionAssert.AreEqual(baseline, withDo);
      Assert.IsTrue(seen.Count > withDo.Count,
        $"Do should observe every visit ({seen.Count}), more than the schedule-only stream ({withDo.Count})");
    }

    [TestMethod]
    public async Task AsyncTree_Empty_YieldsNothing_BothDimensions()
    {
      var empty = AsyncTree.Empty<int>();
      Assert.AreEqual(0, await empty.CountNodesAsync());
      CollectionAssert.AreEqual(Array.Empty<int>(), await empty.ToListAsync());
      CollectionAssert.AreEqual(new Visit[0], await CollectAsync(empty.GetAsyncBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public async Task AsyncTree_Defer_IsLazy_AndBuildsFreshPerAcquisition()
    {
      var acquisitions = 0;
      var deferred = AsyncTree.Defer(() =>
      {
        acquisitions++;
        return (IAsyncTreenumerable<int>)new AsyncTreenumerable<int, int, AsyncChildEnumerator>(
          nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n, AsyncRoots());
      });

      Assert.AreEqual(0, acquisitions, "Defer must not run the factory before a treenumerator is acquired");

      var first = await deferred.ToListAsync();
      var second = await deferred.ToListAsync();

      CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 4, 6, 7 }, first);
      CollectionAssert.AreEqual(first, second);
      Assert.AreEqual(2, acquisitions, "Defer builds a fresh tree per treenumerator acquisition");
    }

    [TestMethod]
    public async Task AsyncTree_Lazy_PinsTheFirstConstruction()
    {
      var constructions = 0;
      var lazyTree = AsyncTree.Lazy(() =>
      {
        constructions++;
        return (IAsyncTreenumerable<int>)new AsyncTreenumerable<int, int, AsyncChildEnumerator>(
          nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n, AsyncRoots());
      });

      Assert.AreEqual(0, constructions, "Lazy must not run the factory before a treenumerator is acquired");

      var first = await lazyTree.ToListAsync();
      var second = await lazyTree.ToListAsync();

      CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 4, 6, 7 }, first);
      CollectionAssert.AreEqual(first, second);
      Assert.AreEqual(1, constructions, "Lazy pins the first construction for every later acquisition");
    }

    [TestMethod]
    public async Task AsyncTree_Lazy_DimensionObservingFactoryRunsOnceWithTheFirstDimension()
    {
      var observedDimensions = new List<TreeTraversalStrategy>();
      var lazyTree = AsyncTree.Lazy(firstDimension =>
      {
        observedDimensions.Add(firstDimension);
        return (IAsyncTreenumerable<int>)new AsyncTreenumerable<int, int, AsyncChildEnumerator>(
          nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)), n => n, AsyncRoots());
      });

      await CollectAsync(lazyTree.GetAsyncBreadthFirstTreenumerator());
      await CollectAsync(lazyTree.GetAsyncDepthFirstTreenumerator());

      CollectionAssert.AreEqual(new[] { TreeTraversalStrategy.BreadthFirst }, observedDimensions);
    }

    [TestMethod]
    public async Task AsyncPreorderStream_OverSuspendingSource_MatchesGeneratedSyncStreamTwin()
    {
      // Preorder (value, depth) for 1(2(4,5),3(6)).
      var nodes = new (int Value, int Depth)[] { (1, 0), (2, 1), (4, 2), (5, 2), (3, 1), (6, 2) };

      var sync = Collect(new PreorderStreamDepthFirstTreenumerator<int, SyncPreorderStream>(
        new SyncPreorderStream(nodes)));

      var async = await CollectAsync(new AsyncPreorderStreamDepthFirstTreenumerator<int, SuspendingPreorderStream>(
        new SuspendingPreorderStream(nodes)));

      CollectionAssert.AreEqual(sync, async);
    }

    [TestMethod]
    public async Task AsyncLevelOrderStream_OverSuspendingSource_MatchesGeneratedSyncStreamTwin()
    {
      // Level-order groups for 1(2(4,5),3(6)): group 0 is the roots, group k+1 is the children of
      // the k-th level-order node; trailing empty groups (the leaves' groups) are elided.
      var groups = new[] { new[] { 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6 } };

      var sync = Collect(new LevelOrderStreamBreadthFirstTreenumerator<int, SyncLevelOrderStream>(
        new SyncLevelOrderStream(groups)));

      var async = await CollectAsync(new AsyncLevelOrderStreamBreadthFirstTreenumerator<int, SuspendingLevelOrderStream>(
        new SuspendingLevelOrderStream(groups)));

      CollectionAssert.AreEqual(sync, async);
    }

    // --- Collection helpers ---

    private readonly record struct Visit(TreenumeratorMode Mode, int Node, int VisitCount, int Depth, int SiblingIndex);

    private static List<Visit> Collect(ITreenumerator<int> t)
    {
      var visits = new List<Visit>();
      using (t)
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          visits.Add(new Visit(t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
      return visits;
    }

    private static async Task<List<Visit>> CollectAsync(IAsyncTreenumerator<int> t)
    {
      var visits = new List<Visit>();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll))
          visits.Add(new Visit(t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
      return visits;
    }

    private static List<int> FirstVisitNodes(List<Visit> visits)
      => visits.Where(v => v.Mode == TreenumeratorMode.VisitingNode && v.VisitCount == 1).Select(v => v.Node).ToList();

    private static async IAsyncEnumerable<int> AsyncRoots()
    {
      foreach (var r in Roots)
      {
        await Task.Yield(); // force real asynchrony on the root seam
        yield return r;
      }
    }

    // --- Child enumerators: sync (out-style), sync Current-style (for the generated drivers), and a
    //     genuinely-suspending async one. ---

    private struct SyncChildEnumerator : IChildEnumerator<int>
    {
      private readonly int[] _children;
      private int _i;
      public SyncChildEnumerator(int[] children) { _children = children; _i = 0; }

      public Copse.ChildResult<int> MoveNext()
      {
        if (_i < _children.Length) { var child = new NodeAndSiblingIndex<int>(_children[_i], _i); _i++; return new Copse.ChildResult<int>(child); }
        return default;
      }

      public void Dispose() { }
    }

    private sealed class AsyncChildEnumerator : IAsyncChildEnumerator<int>
    {
      private readonly int[] _children;
      private int _i;
      public AsyncChildEnumerator(int[] children) { _children = children; }

      public async ValueTask<Copse.Async.ChildResult<int>> MoveNextAsync()
      {
        await Task.Yield(); // force real asynchrony on the child seam
        if (_i < _children.Length) { var r = new Copse.Async.ChildResult<int>(new NodeAndSiblingIndex<int>(_children[_i], _i)); _i++; return r; }
        return default;
      }

      public void Dispose() { }
      public ValueTask DisposeAsync() => default;
    }

    // Preorder-stream doubles: a sync one for the generated twin, and a genuinely-suspending async
    // one (Task.Yield on every read) for the driver. Both replay the same (value, depth) list.
    private struct SyncPreorderStream : Copse.Stores.IPreorderStream<int>
    {
      private readonly (int Value, int Depth)[] _nodes;
      private int _i;
      public SyncPreorderStream((int Value, int Depth)[] nodes) { _nodes = nodes; _i = 0; }

      public Copse.Stores.PreorderRead<int> TryReadNext()
      {
        if (_i >= _nodes.Length) return default;
        var (v, d) = _nodes[_i++];
        return new Copse.Stores.PreorderRead<int>(v, d);
      }

      public Copse.Stores.PreorderRead<int> TrySkipToDepth(int maxDepth)
      {
        while (_i < _nodes.Length && _nodes[_i].Depth > maxDepth) _i++;
        return TryReadNext();
      }

      public void Dispose() { }
    }

    private sealed class SuspendingPreorderStream : IAsyncPreorderStream<int>
    {
      private readonly (int Value, int Depth)[] _nodes;
      private int _i;
      public SuspendingPreorderStream((int Value, int Depth)[] nodes) { _nodes = nodes; }

      public async ValueTask<PreorderRead<int>> TryReadNextAsync()
      {
        await Task.Yield(); // force real asynchrony on the read seam
        if (_i >= _nodes.Length) return default;
        var (v, d) = _nodes[_i++];
        return new PreorderRead<int>(v, d);
      }

      public async ValueTask<PreorderRead<int>> TrySkipToDepthAsync(int maxDepth)
      {
        await Task.Yield();
        while (_i < _nodes.Length && _nodes[_i].Depth > maxDepth) _i++;
        if (_i >= _nodes.Length) return default;
        var (v, d) = _nodes[_i++];
        return new PreorderRead<int>(v, d);
      }

      public ValueTask DisposeAsync() => default;
    }

    // Level-order-stream doubles: replay int[][] groups (group 0 roots, group k+1 = children of
    // node k). Sync for the generated twin, genuinely-suspending async for the driver.
    private struct SyncLevelOrderStream : Copse.Stores.ILevelOrderStream<int>
    {
      private readonly int[][] _groups;
      private int _g, _i;
      public SyncLevelOrderStream(int[][] groups) { _groups = groups; _g = 0; _i = 0; }

      public Copse.Stores.LevelOrderRead<int> TryReadNextInGroup()
      {
        if (_g >= _groups.Length || _i >= _groups[_g].Length) return default;
        return new Copse.Stores.LevelOrderRead<int>(_groups[_g][_i++]);
      }

      public int SkipGroupRemainder()
      {
        if (_g >= _groups.Length) return 0;
        var rem = _groups[_g].Length - _i;
        _i = _groups[_g].Length;
        return rem;
      }

      public bool TryMoveToNextGroup()
      {
        if (_g + 1 >= _groups.Length) return false;
        _g++; _i = 0; return true;
      }

      public void Dispose() { }
    }

    private sealed class SuspendingLevelOrderStream : IAsyncLevelOrderStream<int>
    {
      private readonly int[][] _groups;
      private int _g, _i;
      public SuspendingLevelOrderStream(int[][] groups) { _groups = groups; }

      public async ValueTask<LevelOrderRead<int>> TryReadNextInGroupAsync()
      {
        await Task.Yield();
        if (_g >= _groups.Length || _i >= _groups[_g].Length) return default;
        return new LevelOrderRead<int>(_groups[_g][_i++]);
      }

      public async ValueTask<int> SkipGroupRemainderAsync()
      {
        await Task.Yield();
        if (_g >= _groups.Length) return 0;
        var rem = _groups[_g].Length - _i;
        _i = _groups[_g].Length;
        return rem;
      }

      public async ValueTask<bool> TryMoveToNextGroupAsync()
      {
        await Task.Yield();
        if (_g + 1 >= _groups.Length) return false;
        _g++; _i = 0; return true;
      }

      public ValueTask DisposeAsync() => default;
    }
  }
}
