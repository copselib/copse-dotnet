using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS checks for AsyncTree's Using/Defer families (the sync twin's logic rides the
  // sync Tree suites): acquisition at treenumerator acquisition, release on treenumerator
  // disposal with the IAsyncDisposable preference, and cleanup when tree construction throws
  // after acquisition.
  [TestClass]
  public class AsyncTreeUsingTests
  {
    [TestMethod]
    public async Task Using_AcquiresAtGetter_ReleasesOnDispose_PrefersAsyncDisposal()
    {
      var resources = new List<TestResource>();

      var tree = AsyncTree.Using(
        () => { var resource = new TestResource(); resources.Add(resource); return resource; },
        _ => Async("a(b,c)"));

      Assert.AreEqual(0, resources.Count, "nothing acquired before treenumerator acquisition");

      var treenumerator = tree.GetAsyncDepthFirstTreenumerator();
      Assert.AreEqual(1, resources.Count, "Using acquires at treenumerator acquisition");
      Assert.IsFalse(resources[0].IsDisposed, "resource lives while the traversal does");

      while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll)) { }
      Assert.IsFalse(resources[0].IsDisposed, "exhaustion does not release; disposal does");

      await treenumerator.DisposeAsync();
      Assert.IsTrue(resources[0].AsyncDisposed, "a resource implementing IAsyncDisposable gets DisposeAsync");
      Assert.IsFalse(resources[0].SyncDisposed, "the async preference means Dispose is not also called");
    }

    [TestMethod]
    public async Task Using_EachAcquisitionOwnsItsResource_TraversalMatchesUnwrappedTree()
    {
      var expected = await Drain(Async("a(b(d,e),c)"));

      var resources = new List<TestResource>();
      var tree = AsyncTree.Using(
        () => { var resource = new TestResource(); resources.Add(resource); return resource; },
        _ => Async("a(b(d,e),c)"));

      CollectionAssert.AreEqual(expected, await Drain(tree), "first traversal");
      CollectionAssert.AreEqual(expected, await Drain(tree), "second traversal");

      Assert.AreEqual(2, resources.Count, "each treenumerator acquisition acquires its OWN resource");
      Assert.IsTrue(resources[0].IsDisposed && resources[1].IsDisposed, "each traversal released its resource");
    }

    [TestMethod]
    public void Using_TreeConstructionThrows_ResourceIsReleased()
    {
      var resource = new TestResource();
      var tree = AsyncTree.Using<TestResource, string>(
        () => resource,
        _ => throw new InvalidOperationException("boom"));

      Assert.ThrowsException<InvalidOperationException>(() => tree.GetAsyncDepthFirstTreenumerator());
      Assert.IsTrue(resource.IsDisposed, "the failure path releases (synchronously, by design)");
    }

    [TestMethod]
    public async Task UsingNarrow_And_DeferNarrow_ServeTheirDimension()
    {
      var expected = await Drain(Async("a(b,c)"));

      var usingNarrow = AsyncTree.UsingDepthFirst(
        () => new TestResource(),
        _ => (IAsyncDepthFirstTreenumerable<string>)Async("a(b,c)"));

      var deferred = AsyncTree.DeferDepthFirst(
        () => (IAsyncDepthFirstTreenumerable<string>)Async("a(b,c)"));

      CollectionAssert.AreEqual(expected, await DrainNarrow(usingNarrow), "UsingDepthFirst");
      CollectionAssert.AreEqual(expected, await DrainNarrow(deferred), "DeferDepthFirst");
    }

    private static async Task<List<string>> Drain(IAsyncTreenumerable<string> tree)
    {
      var visits = new List<string>();
      var treenumerator = tree.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll))
          visits.Add($"{treenumerator.Mode}:{treenumerator.Node}:{treenumerator.VisitCount}:{treenumerator.Position}");
      return visits;
    }

    private static async Task<List<string>> DrainNarrow(IAsyncDepthFirstTreenumerable<string> tree)
    {
      var visits = new List<string>();
      var treenumerator = tree.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll))
          visits.Add($"{treenumerator.Mode}:{treenumerator.Node}:{treenumerator.VisitCount}:{treenumerator.Position}");
      return visits;
    }

    private static IAsyncTreenumerable<string> Async(string tree)
      => new AsyncFacade(TreeSerializer.DeserializeDepthFirstTree(tree));

    private sealed class TestResource : IDisposable, IAsyncDisposable
    {
      public bool SyncDisposed { get; private set; }
      public bool AsyncDisposed { get; private set; }
      public bool IsDisposed => SyncDisposed || AsyncDisposed;
      public void Dispose() => SyncDisposed = true;
      public ValueTask DisposeAsync() { AsyncDisposed = true; return default; }
    }

    // A completed-ValueTask facade over a sync composite treenumerable.
    private sealed class AsyncFacade : IAsyncTreenumerable<string>
    {
      private readonly ITreenumerable<string> _Source;
      public AsyncFacade(ITreenumerable<string> source) { _Source = source; }
      public IAsyncTreenumerator<string> GetAsyncDepthFirstTreenumerator() => new Cursor(_Source.GetDepthFirstTreenumerator());
      public IAsyncTreenumerator<string> GetAsyncBreadthFirstTreenumerator() => new Cursor(_Source.GetBreadthFirstTreenumerator());

      private sealed class Cursor : IAsyncTreenumerator<string>
      {
        private readonly ITreenumerator<string> _Inner;
        public Cursor(ITreenumerator<string> inner) { _Inner = inner; }
        public string Node => _Inner.Node;
        public int VisitCount => _Inner.VisitCount;
        public TreenumeratorMode Mode => _Inner.Mode;
        public NodePosition Position => _Inner.Position;
        public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s) => new ValueTask<bool>(_Inner.MoveNext(s));
        public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
      }
    }
  }
}
