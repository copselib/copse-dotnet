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
  // Thin MECHANICS check for the async memo (the logic is tested once, on the generated sync
  // twins, by MemoizeTests + the conformance matrix): the async memo must capture from a
  // genuinely-suspending source, replay both dimensions without re-running it, honor the
  // single-shape invariant, and retire the feed on disposal.
  [TestClass]
  public class AsyncMemoizeTests
  {
    private static readonly string[] Trees =
    {
      "a", "a(b,c,d)", "a(b(e),c)", "a,b,c", "a(b(d,e),c)", "a(b(d,e,f),c(g,h,i))",
    };

    [TestMethod]
    public async Task Memoize_ReplaysBothDimensions_SourceEnumeratedOnce()
    {
      foreach (var tree in Trees)
      {
        var counting = new CountingAsyncSource<string>(Sync(tree));
        var memo = counting.Memoize();

        // Two replays per dimension: the second rides the capture.
        for (var i = 0; i < 2; i++)
        {
          CollectionAssert.AreEqual(Sync(tree).PreorderTraversal().ToList(), await ToList(memo.PreorderTraversal()), $"Preorder#{i} {tree}");
          CollectionAssert.AreEqual(Sync(tree).LevelOrderTraversal().ToList(), await ToList(memo.LevelOrderTraversal()), $"LevelOrder#{i} {tree}");
        }

        // The first replay's dimension completed the capture, so the second dimension rides it
        // cross-order: the source is opened exactly once.
        Assert.AreEqual(1, counting.TreenumeratorsCreated, $"source enumerations {tree}");
        Assert.IsTrue(memo.IsComplete, $"IsComplete {tree}");
      }
    }

    [TestMethod]
    public async Task MaterializeAsync_CompletesCapture_AndReplays()
    {
      foreach (var tree in Trees)
      {
        var buffer = await Async(tree).MaterializeAsync();

        CollectionAssert.AreEqual(Sync(tree).PreorderTraversal().ToList(), await ToList(buffer.PreorderTraversal()), $"Preorder {tree}");
        CollectionAssert.AreEqual(Sync(tree).LevelOrderTraversal().ToList(), await ToList(buffer.LevelOrderTraversal()), $"LevelOrder {tree}");
      }
    }

    [TestMethod]
    public async Task Memoize_GenuinelySuspendingSource_BothDimensionsCorrect()
    {
      foreach (var tree in Trees)
      {
        var memo = new YieldingAsyncSource<string>(Sync(tree)).Memoize();

        CollectionAssert.AreEqual(Sync(tree).LevelOrderTraversal().ToList(), await ToList(memo.LevelOrderTraversal()), $"LevelOrder {tree}");
        CollectionAssert.AreEqual(Sync(tree).PreorderTraversal().ToList(), await ToList(memo.PreorderTraversal()), $"Preorder {tree}");
      }
    }

    [TestMethod]
    public async Task Memoize_IsLazy_AndIdempotent()
    {
      var counting = new CountingAsyncSource<string>(Sync("a(b,c)"));
      var memo = counting.Memoize();

      Assert.AreEqual(0, counting.TreenumeratorsCreated, "no pull before first replay demand");
      Assert.AreSame(memo, memo.Memoize(), "memoizing a live memo returns it unchanged");

      await memo.ConsumeAsync();
      Assert.AreEqual(1, counting.TreenumeratorsCreated);
      Assert.IsTrue(memo.IsComplete);
    }

    [TestMethod]
    public async Task DisposeAsync_RetiresFeed_ReplayPastFrontierThrows()
    {
      var memo = Async("a(b(d,e),c)").Memoize();

      // Buffer a prefix, then retire the feed.
      var t = memo.GetAsyncDepthFirstTreenumerator();
      await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll); // S a
      await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll); // V a
      await t.DisposeAsync();

      await memo.DisposeAsync();

      await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
      {
        await memo.ConsumeAsync();
      });
    }

    private static async Task<List<T>> ToList<T>(IAsyncEnumerable<T> source)
    {
      var list = new List<T>();
      await foreach (var x in source.ConfigureAwait(false))
        list.Add(x);
      return list;
    }

    private static ITreenumerable<string> Sync(string tree) => TreeSerializer.DeserializeDepthFirstTree(tree);
    private static IAsyncTreenumerable<string> Async(string tree) => new CountingAsyncSource<string>(Sync(tree));

    // A completed-ValueTask facade over a sync composite treenumerable that counts how many
    // treenumerators it hands out -- the observable for "the source is never touched again".
    private sealed class CountingAsyncSource<T> : IAsyncTreenumerable<T>
    {
      private readonly ITreenumerable<T> _Source;
      public CountingAsyncSource(ITreenumerable<T> source) { _Source = source; }
      public int TreenumeratorsCreated { get; private set; }
      public IAsyncTreenumerator<T> GetAsyncDepthFirstTreenumerator() { TreenumeratorsCreated++; return new Cursor<T>(_Source.GetDepthFirstTreenumerator(), yielding: false); }
      public IAsyncTreenumerator<T> GetAsyncBreadthFirstTreenumerator() { TreenumeratorsCreated++; return new Cursor<T>(_Source.GetBreadthFirstTreenumerator(), yielding: false); }
    }

    // A genuinely-suspending facade: Task.Yield on every pull, so the memo's awaits actually suspend.
    private sealed class YieldingAsyncSource<T> : IAsyncTreenumerable<T>
    {
      private readonly ITreenumerable<T> _Source;
      public YieldingAsyncSource(ITreenumerable<T> source) { _Source = source; }
      public IAsyncTreenumerator<T> GetAsyncDepthFirstTreenumerator() => new Cursor<T>(_Source.GetDepthFirstTreenumerator(), yielding: true);
      public IAsyncTreenumerator<T> GetAsyncBreadthFirstTreenumerator() => new Cursor<T>(_Source.GetBreadthFirstTreenumerator(), yielding: true);
    }

    private sealed class Cursor<T> : IAsyncTreenumerator<T>
    {
      private readonly ITreenumerator<T> _Inner;
      private readonly bool _Yielding;
      public Cursor(ITreenumerator<T> inner, bool yielding) { _Inner = inner; _Yielding = yielding; }
      public T Node => _Inner.Node;
      public int VisitCount => _Inner.VisitCount;
      public TreenumeratorMode Mode => _Inner.Mode;
      public NodePosition Position => _Inner.Position;
      public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s)
      {
        if (_Yielding)
          await Task.Yield(); // force real asynchrony on the pull seam
        return _Inner.MoveNext(s);
      }
      public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
    }
  }
}
