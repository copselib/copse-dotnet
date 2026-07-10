using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS check for the async capture operators LeaffixScan and Invert (the logic is
  // tested once, on the generated sync twins): each must agree with its sync counterpart over
  // the same tree, from a genuinely-suspending source, in both traversal dimensions.
  [TestClass]
  public class AsyncCaptureOpsTests
  {
    private static readonly string[] Trees =
    {
      "a", "a(b,c,d)", "a(b(e),c)", "a,b,c", "a(b(d,e),c)", "a(b(d,e,f),c(g,h,i))",
    };

    [TestMethod]
    public async Task LeaffixScan_MatchesSync_BothDimensions()
    {
      foreach (var tree in Trees)
      {
        var sync = Sync(tree).LeaffixScan(
          nc => nc.Node,
          (nc, kids) => nc.Node + "(" + string.Join(",", kids) + ")");

        var async = Async(tree).LeaffixScan(
          nc => nc.Node,
          (nc, kids) => nc.Node + "(" + string.Join(",", kids) + ")");

        CollectionAssert.AreEqual(sync.PreorderTraversal().ToList(), await ToList(async.PreorderTraversal()), $"Preorder {tree}");
        CollectionAssert.AreEqual(sync.LevelOrderTraversal().ToList(), await ToList(async.LevelOrderTraversal()), $"LevelOrder {tree}");
      }
    }

    [TestMethod]
    public async Task Invert_FullSource_MatchesSync_BothDimensions()
    {
      foreach (var tree in Trees)
      {
        var sync = Sync(tree).Invert();
        var async = Async(tree).Invert();

        CollectionAssert.AreEqual(sync.PreorderTraversal().ToList(), await ToList(async.PreorderTraversal()), $"Preorder {tree}");
        CollectionAssert.AreEqual(sync.LevelOrderTraversal().ToList(), await ToList(async.LevelOrderTraversal()), $"LevelOrder {tree}");
      }
    }

    [TestMethod]
    public async Task Invert_FullSource_BreadthFirstFirst_PinsTheStreamedCapture()
    {
      foreach (var tree in Trees)
      {
        var sync = Sync(tree).Invert();
        var async = Async(tree).Invert();

        // Breadth-first pulled FIRST pins the memoized streaming mirror; the depth-first
        // replay then rides the same capture (the reverse pin order of the test above).
        CollectionAssert.AreEqual(sync.LevelOrderTraversal().ToList(), await ToList(async.LevelOrderTraversal()), $"LevelOrder {tree}");
        CollectionAssert.AreEqual(sync.PreorderTraversal().ToList(), await ToList(async.PreorderTraversal()), $"Preorder {tree}");
      }
    }

    [TestMethod]
    public async Task Invert_NarrowBreadthFirst_StreamsMirroredLevels()
    {
      foreach (var tree in Trees)
      {
        var sync = ((IBreadthFirstTreenumerable<string>)Sync(tree)).Invert();
        var async = ((IAsyncBreadthFirstTreenumerable<string>)Async(tree)).Invert();

        CollectionAssert.AreEqual(sync.LevelOrderTraversal().ToList(), await ToList(async.LevelOrderTraversal()), $"LevelOrder {tree}");
      }
    }

    [TestMethod]
    public async Task Invert_Buffer_MirrorsWithoutReenumeratingSource()
    {
      foreach (var tree in Trees)
      {
        var buffer = await Async(tree).MaterializeAsync();
        var mirrored = buffer.Invert();

        var sync = Sync(tree).Materialize().Invert();

        CollectionAssert.AreEqual(sync.PreorderTraversal().ToList(), await ToList(mirrored.PreorderTraversal()), $"Preorder {tree}");
      }
    }

    private static async Task<List<T>> ToList<T>(IAsyncEnumerable<T> source)
    {
      var list = new List<T>();
      await foreach (var x in source.ConfigureAwait(false))
        list.Add(x);
      return list;
    }

    private static ITreenumerable<string> Sync(string tree) => TreeSerializer.DeserializeDepthFirstTree(tree);
    private static IAsyncTreenumerable<string> Async(string tree) => new YieldingAsyncSource<string>(Sync(tree));

    // A genuinely-suspending facade: Task.Yield on every pull, so the operators' awaits actually suspend.
    private sealed class YieldingAsyncSource<T> : IAsyncTreenumerable<T>
    {
      private readonly ITreenumerable<T> _Source;
      public YieldingAsyncSource(ITreenumerable<T> source) { _Source = source; }
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
        public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s)
        {
          await Task.Yield(); // force real asynchrony on the pull seam
          return _Inner.MoveNext(s);
        }
        public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
      }
    }
  }
}
