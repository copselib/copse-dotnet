using Copse.Core;
using Copse.Treenumerables;
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
      => Tree.Using(
        () =>
        {
          var resource = new TestResource();
          resources.Add(resource);
          return resource;
        },
        _ => TreeSerializer.DeserializeDepthFirstTree(tree));

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

      UsingTree("a(b,c)", resources).GetPreorderTraversal().ToArray();

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

      tree.GetPreorderTraversal().ToArray();
      tree.GetLevelOrderTraversal().ToArray();

      Assert.AreEqual(2, resources.Count);
      Assert.IsTrue(resources.All(resource => resource.DisposeCount == 1));
    }

    [TestMethod]
    public void TreeFactoryThrowing_DisposesResourceAndPropagates()
    {
      var resources = new List<TestResource>();

      var tree = Tree.Using<TestResource, string>(
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

      // Materialize is deferred (2026-08-10): nothing opens at the call, so no resource exists
      // yet -- an unconsumed result holds exactly what the unconsumed pipeline held.
      Assert.AreEqual(0, resources.Count);

      // The first pull runs the whole capture: the resource is acquired and released inside it.
      materialized.GetPreorderTraversal().ToArray();

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);

      // Replays ride the capture; the source (and its resource) is never touched again.
      materialized.GetPreorderTraversal().ToArray();
      materialized.GetLevelOrderTraversal().ToArray();

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
        var direct = TreeSerializer.DeserializeDepthFirstTree(tree);
        var wrapped = Tree.Using(
          () => new TestResource(),
          _ => TreeSerializer.DeserializeDepthFirstTree(tree));

        CollectionAssert.AreEqual(
          direct.GetPreorderTraversal().ToArray(),
          wrapped.GetPreorderTraversal().ToArray(),
          $"Preorder mismatch for {tree}");

        CollectionAssert.AreEqual(
          direct.GetLevelOrderTraversal().ToArray(),
          wrapped.GetLevelOrderTraversal().ToArray(),
          $"LevelOrder mismatch for {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // Narrow-dimension resource ownership: the result's dimension follows the tree handed in,
    // and disposal is tied to that single dimension's treenumerator (a full Tree.Using would
    // over-generalize a forward-only source -- the serializer's motivating case).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void UsingDepthFirstReturnsDepthFirstAndOwnsTheResource()
    {
      var resources = new List<TestResource>();

      IDepthFirstTreenumerable<string> tree = Tree.UsingDepthFirst<TestResource, string>(
        () => { var r = new TestResource(); resources.Add(r); return r; },
        _ => TreeSerializer.DeserializeDepthFirstTree("a(b,c)"));

      Assert.AreEqual(0, resources.Count); // lazy: nothing acquired at composition

      tree.GetPreorderTraversal().ToArray();

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void UsingBreadthFirstReturnsBreadthFirstAndOwnsTheResource()
    {
      var resources = new List<TestResource>();

      IBreadthFirstTreenumerable<string> tree = Tree.UsingBreadthFirst<TestResource, string>(
        () => { var r = new TestResource(); resources.Add(r); return r; },
        _ => TreeSerializer.DeserializeBreadthFirstTree("a;b,c"));

      tree.GetLevelOrderTraversal().ToArray();

      Assert.AreEqual(1, resources.Count);
      Assert.AreEqual(1, resources[0].DisposeCount);
    }

    [TestMethod]
    public void DeferDepthFirstAndBreadthFirstRunTheFactoryPerAcquisition()
    {
      var depthFirstCalls = 0;
      IDepthFirstTreenumerable<string> dft = Tree.DeferDepthFirst(() => { depthFirstCalls++; return TreeSerializer.DeserializeDepthFirstTree("a(b,c)"); });

      dft.GetPreorderTraversal().ToArray();
      dft.GetPreorderTraversal().ToArray();
      Assert.AreEqual(2, depthFirstCalls);

      var breadthFirstCalls = 0;
      IBreadthFirstTreenumerable<string> bft = Tree.DeferBreadthFirst(() => { breadthFirstCalls++; return TreeSerializer.DeserializeBreadthFirstTree("a;b,c"); });

      bft.GetLevelOrderTraversal().ToArray();
      Assert.AreEqual(1, breadthFirstCalls);
    }
  }
}
