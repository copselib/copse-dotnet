using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // The async terminals / queries must agree with their sync counterparts over the same tree (async
  // source is a completed-ValueTask facade over the sync composite deserialize).
  [TestClass]
  public class AsyncQueryTests
  {
    private static readonly string[] Trees =
    {
      "a", "a(b,c,d)", "a(b(e),c)", "a,b,c", "a(b(d,e),c)", "a(b(d,e,f),c(g,h,i))",
    };

    [TestMethod]
    public async Task AnyNodes_AllNodes_MatchSync()
    {
      foreach (var tree in Trees)
      {
        Func<NodeContext<string>, bool> hasB = nc => nc.Node == "b";
        Assert.AreEqual(Sync(tree).AnyNodes(hasB), await Async(tree).AnyNodesAsync(hasB), $"AnyNodes {tree}");
        Assert.AreEqual(Sync(tree).AnyNodes(nc => nc.Node == "z"), await Async(tree).AnyNodesAsync(nc => nc.Node == "z"), $"AnyNodes-none {tree}");

        Func<NodeContext<string>, bool> notZ = nc => nc.Node != "z";
        Assert.AreEqual(Sync(tree).AllNodes(notZ), await Async(tree).AllNodesAsync(notZ), $"AllNodes {tree}");
        Assert.AreEqual(Sync(tree).AllNodes(hasB), await Async(tree).AllNodesAsync(hasB), $"AllNodes-not {tree}");
      }
    }

    [TestMethod]
    public async Task CountTrees_And_GetRoots_MatchSync()
    {
      foreach (var tree in Trees)
      {
        Assert.AreEqual(Sync(tree).CountTrees(), await Async(tree).CountTreesAsync(), $"CountTrees {tree}");

        var syncRoots = Sync(tree).GetRoots().ToList();
        var asyncRoots = new List<string>();
        await foreach (var r in Async(tree).GetRoots())
          asyncRoots.Add(r);
        CollectionAssert.AreEqual(syncRoots, asyncRoots, $"GetRoots {tree}");
      }
    }

    [TestMethod]
    public async Task Consume_DrainsWithoutError()
    {
      foreach (var tree in Trees)
        await Async(tree).ConsumeAsync();
    }

    [TestMethod]
    public async Task Traversals_And_GetLeaves_MatchSync()
    {
      foreach (var tree in Trees)
      {
        CollectionAssert.AreEqual(Sync(tree).PreorderTraversal().ToList(), await ToList(Async(tree).PreorderTraversal()), $"Preorder {tree}");
        CollectionAssert.AreEqual(Sync(tree).PostorderTraversal().ToList(), await ToList(Async(tree).PostorderTraversal()), $"Postorder {tree}");
        CollectionAssert.AreEqual(Sync(tree).LevelOrderTraversal().ToList(), await ToList(Async(tree).LevelOrderTraversal()), $"LevelOrder {tree}");

        CollectionAssert.AreEqual(Sync(tree).GetLeaves().ToList(), await ToList(Async(tree).GetLeaves()), $"GetLeaves-dft {tree}");
        CollectionAssert.AreEqual(
          ((IBreadthFirstTreenumerable<string>)Sync(tree)).GetLeaves().ToList(),
          await ToList(((IAsyncBreadthFirstTreenumerable<string>)Async(tree)).GetLeaves()),
          $"GetLeaves-bft {tree}");
      }
    }

    [TestMethod]
    public async Task GetLevels_And_GetBranches_MatchSync()
    {
      foreach (var tree in Trees)
      {
        CollectionAssert.AreEqual(
          Sync(tree).GetLevels().Select(a => string.Join(",", a)).ToList(),
          (await ToList(Async(tree).GetLevels())).Select(a => string.Join(",", a)).ToList(),
          $"GetLevels {tree}");

        CollectionAssert.AreEqual(
          Sync(tree).GetBranches().Select(a => string.Join(",", a)).ToList(),
          (await ToList(Async(tree).GetBranches())).Select(a => string.Join(",", a)).ToList(),
          $"GetBranches {tree}");
      }
    }

    [TestMethod]
    public async Task RootfixAggregate_And_VisitStreamTraversals_MatchSync()
    {
      Func<NodeContext<string>, NodeContext<string>, string> concat = (acc, node) => acc.Node + node.Node;

      foreach (var tree in Trees)
      {
        CollectionAssert.AreEqual(
          Sync(tree).RootfixAggregate(concat, "").ToList(),
          await ToList(Async(tree).RootfixAggregate(concat, "")),
          $"RootfixAggregate {tree}");

        CollectionAssert.AreEqual(
          Sync(tree).GetDepthFirstTraversal().Select(Fmt).ToList(),
          (await ToList(Async(tree).GetDepthFirstTraversal())).Select(Fmt).ToList(),
          $"GetDepthFirstTraversal {tree}");

        CollectionAssert.AreEqual(
          Sync(tree).GetBreadthFirstTraversal().Select(Fmt).ToList(),
          (await ToList(Async(tree).GetBreadthFirstTraversal())).Select(Fmt).ToList(),
          $"GetBreadthFirstTraversal {tree}");
      }

      static string Fmt(NodeVisit<string> v) => $"{v.Mode}:{v.Node}:{v.VisitCount}:{v.Position.Depth},{v.Position.SiblingIndex}";
    }

    [TestMethod]
    public async Task LeaffixAggregate_MatchesSync()
    {
      // Leaf count per root tree: leaves count 1, internal nodes sum their children's counts.
      Func<NodeContext<string>, int> leaf = _ => 1;
      Func<NodeContext<string>, ChildAccumulations<int>, int> acc = (_, kids) =>
      {
        var sum = 0;
        foreach (var k in kids)
          sum += k;
        return sum;
      };

      foreach (var tree in Trees)
        CollectionAssert.AreEqual(
          Sync(tree).LeaffixAggregate(acc, leaf).ToList(),
          await ToList(Async(tree).LeaffixAggregate(acc, leaf)),
          $"LeaffixAggregate {tree}");
    }

    [TestMethod]
    public async Task TreeSlicing_MatchesSync()
    {
      var forests = new[] { "a,b,c,d", "a(x,y),b,c(z),d", "a" };

      foreach (var tree in forests)
        for (var count = 0; count <= 4; count++)
        {
          CollectionAssert.AreEqual(
            await ToList(Sync(tree).SkipTrees(count).PreorderTraversal()),
            await ToList(Async(tree).SkipTrees(count).PreorderTraversal()),
            $"SkipTrees {tree} {count}");

          CollectionAssert.AreEqual(
            await ToList(Sync(tree).TakeTrees(count).PreorderTraversal()),
            await ToList(Async(tree).TakeTrees(count).PreorderTraversal()),
            $"TakeTrees {tree} {count}");

          CollectionAssert.AreEqual(
            await ToList(Sync(tree).SkipLastTrees(count).PreorderTraversal()),
            await ToList((await Async(tree).SkipLastTreesAsync(count)).PreorderTraversal()),
            $"SkipLastTrees {tree} {count}");

          CollectionAssert.AreEqual(
            await ToList(Sync(tree).TakeLastTrees(count).PreorderTraversal()),
            await ToList((await Async(tree).TakeLastTreesAsync(count)).PreorderTraversal()),
            $"TakeLastTrees {tree} {count}");
        }
    }

    [TestMethod]
    public async Task Formatting_MatchesSync()
    {
      var trees = new[] { "a", "a(b,c,d)", "a(b(e),c)", "a,b,c", "a(b(d,e),c)", "a(b(d,e,f),c(g,h,i))" };

      foreach (var tree in trees)
      {
        CollectionAssert.AreEqual(
          Sync(tree).ToFormattedLines(2).ToList(),
          (await Async(tree).ToFormattedLinesAsync(2)).ToList(),
          $"ToFormattedLines {tree}");

        Assert.AreEqual(
          Sync(tree).ToFormattedString(1),
          await Async(tree).ToFormattedStringAsync(1),
          $"ToFormattedString {tree}");
      }
    }

    // Sync PreorderTraversal returns IEnumerable; bridge it to the async ToList helper.
    private static Task<List<string>> ToList(IEnumerable<string> source) => Task.FromResult(source.ToList());

    private static async Task<List<T>> ToList<T>(IAsyncEnumerable<T> source)
    {
      var list = new List<T>();
      await foreach (var x in source.ConfigureAwait(false))
        list.Add(x);
      return list;
    }

    private static ITreenumerable<string> Sync(string tree) => TreeSerializer.DeserializeDepthFirstTree(tree);
    private static IAsyncTreenumerable<string> Async(string tree) => new AsyncFacade<string>(Sync(tree));

    // A completed-ValueTask facade over a sync composite treenumerable (each pull resolves synchronously).
    private sealed class AsyncFacade<T> : IAsyncTreenumerable<T>
    {
      private readonly ITreenumerable<T> _Source;
      public AsyncFacade(ITreenumerable<T> source) { _Source = source; }
      public IAsyncTreenumerator<T> GetAsyncDepthFirstTreenumerator() => new Cursor(_Source.GetDepthFirstTreenumerator());
      public IAsyncTreenumerator<T> GetAsyncBreadthFirstTreenumerator() => new Cursor(_Source.GetBreadthFirstTreenumerator());

      private sealed class Cursor : IAsyncTreenumerator<T>
      {
        private readonly ITreenumerator<T> _Inner;
        public Cursor(ITreenumerator<T> inner) { _Inner = inner; }
        public T Node => _Inner.Node;
        public int VisitCount => _Inner.VisitCount;
        public TreenumeratorMode Mode => _Inner.Mode;
        public NodePosition Position => _Inner.Position;
        public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s) => new ValueTask<bool>(_Inner.MoveNext(s));
        public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
      }
    }
  }
}
