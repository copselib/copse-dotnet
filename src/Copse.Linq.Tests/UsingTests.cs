using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class UsingTests
  {
    private sealed class TestResource : IDisposable
    {
      public int DisposeCount { get; private set; }
      public bool Disposed => DisposeCount > 0;
      public void Dispose() => DisposeCount++;
    }

    private static ITreenumerable<string> UsingTree(string tree, List<TestResource> resources)
      => Treenumerable.Using(
        () =>
        {
          var resource = new TestResource();
          resources.Add(resource);
          return resource;
        },
        _ => TreeSerializer.Deserialize(tree));

    [TestMethod]
    public void ResourceNotAcquiredUntilEnumeration()
    {
      var resources = new List<TestResource>();

      UsingTree("a(b,c)", resources);

      Assert.AreEqual(0, resources.Count);
    }

    [TestMethod]
    public void ResourceDisposedExactlyOnceWhenTraversalCompletes()
    {
      var resources = new List<TestResource>();

      UsingTree("a(b,c)", resources).PreOrderTraversal().ToArray();

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void ResourceHeldWhileTraversalIsLive()
    {
      var resources = new List<TestResource>();

      using (var treenumerator = UsingTree("a(b,c)", resources).GetDepthFirstTreenumerator())
      {
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

        Assert.AreEqual(1, resources.Count);
        Assert.IsFalse(resources[0].Disposed);
      }

      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void DoubleDisposeReleasesResourceOnce()
    {
      var resources = new List<TestResource>();

      var treenumerator = UsingTree("a", resources).GetDepthFirstTreenumerator();
      treenumerator.Dispose();
      treenumerator.Dispose();

      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void EachDimensionAcquiresItsOwnResource()
    {
      var resources = new List<TestResource>();
      var tree = UsingTree("a(b,c)", resources);

      tree.PreOrderTraversal().ToArray();
      tree.LevelOrderTraversal().ToArray();

      Assert.AreEqual(2, resources.Count);
      Assert.IsTrue(resources.All(resource => resource.DisposeCount == 1));
    }

    [TestMethod]
    public void TreeFactoryThrowing_DisposesResourceAndPropagates()
    {
      var resources = new List<TestResource>();

      var tree = Treenumerable.Using<TestResource, string>(
        () =>
        {
          var resource = new TestResource();
          resources.Add(resource);
          return resource;
        },
        _ => throw new InvalidOperationException("construction failed"));

      Assert.ThrowsException<InvalidOperationException>(
        () => tree.GetDepthFirstTreenumerator());

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void MaterializeReleasesTheResource()
    {
      var resources = new List<TestResource>();

      var materialized = UsingTree("a(b(d,e,f),c(g,h,i))", resources).Materialize();

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);

      // Replays ride the capture; the source (and its resource) is never touched again.
      materialized.PreOrderTraversal().ToArray();
      materialized.LevelOrderTraversal().ToArray();

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void DisposingTheMemoMidCapture_ReleasesTheResource()
    {
      var resources = new List<TestResource>();

      var memo = UsingTree("a(b(d,e,f),c(g,h,i))", resources).Memoize();

      using (var treenumerator = memo.GetDepthFirstTreenumerator())
      {
        // Pull a couple of visits: enough to open the memo's feed (acquiring the resource),
        // nowhere near enough to complete the capture.
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

        Assert.AreEqual(1, resources.Count);
        Assert.IsFalse(resources[0].Disposed);

        // Disposing the memo mid-capture kills the paused feed; the feed is the treenumerator
        // holding the resource, so the resource releases now -- not at capture completion.
        memo.Dispose();

        Assert.AreEqual(1, resources[0].DisposeCount);
      }
    }

    [TestMethod]
    public void TraversalsMatchTheInnerTree()
    {
      var trees = new[] { "a", "a(b(c))", "a(b,c)", "a,b,c", "a(b(d,e,f),c(g,h,i))" };

      foreach (var tree in trees)
      {
        var direct = TreeSerializer.Deserialize(tree);
        var wrapped = Treenumerable.Using(
          () => new TestResource(),
          _ => TreeSerializer.Deserialize(tree));

        CollectionAssert.AreEqual(
          direct.PreOrderTraversal().ToArray(),
          wrapped.PreOrderTraversal().ToArray(),
          $"PreOrder mismatch for {tree}");

        CollectionAssert.AreEqual(
          direct.LevelOrderTraversal().ToArray(),
          wrapped.LevelOrderTraversal().ToArray(),
          $"LevelOrder mismatch for {tree}");
      }
    }
  }
}
