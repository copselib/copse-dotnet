using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class LazyTests
  {
    [TestMethod]
    public void FactoryNotInvokedUntilAcquisition()
    {
      var invocations = 0;

      var lazyTree = Tree.Lazy(() =>
      {
        invocations++;
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      Assert.AreEqual(0, invocations);

      lazyTree.PreorderTraversal().ToArray();

      Assert.AreEqual(1, invocations);
    }

    [TestMethod]
    public void FactoryInvokedOnceAcrossAcquisitionsAndDimensions()
    {
      var invocations = 0;

      var lazyTree = Tree.Lazy(() =>
      {
        invocations++;
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      lazyTree.PreorderTraversal().ToArray();
      lazyTree.PreorderTraversal().ToArray();
      lazyTree.LevelOrderTraversal().ToArray();

      Assert.AreEqual(1, invocations);
    }

    [TestMethod]
    public void ImpureFactoryStillYieldsOneTree()
    {
      // Under Defer each traversal would see a different tree; Lazy pins the first.
      var built = 0;

      var lazyTree = Tree.Lazy(
        () => TreeSerializer.DeserializeDepthFirstTree(built++ == 0 ? "a(b,c)" : "x"));

      var breadthFirst = lazyTree.LevelOrderTraversal().ToArray();
      var depthFirst = lazyTree.PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { "a", "b", "c" }, breadthFirst);
      CollectionAssert.AreEqual(new[] { "a", "b", "c" }, depthFirst);
    }

    [TestMethod]
    public void DimensionObservingFactorySeesBreadthFirstWhenAskedFirst()
    {
      var observedDimensions = new List<TreeTraversalStrategy>();

      var lazyTree = Tree.Lazy(firstDimension =>
      {
        observedDimensions.Add(firstDimension);
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      lazyTree.LevelOrderTraversal().ToArray();
      lazyTree.PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { TreeTraversalStrategy.BreadthFirst }, observedDimensions);
    }

    [TestMethod]
    public void DimensionObservingFactorySeesDepthFirstWhenAskedFirst()
    {
      var observedDimensions = new List<TreeTraversalStrategy>();

      var lazyTree = Tree.Lazy(firstDimension =>
      {
        observedDimensions.Add(firstDimension);
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      lazyTree.PreorderTraversal().ToArray();
      lazyTree.LevelOrderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { TreeTraversalStrategy.DepthFirst }, observedDimensions);
    }

    [TestMethod]
    public void TraversalsMatchTheInnerTree()
    {
      var trees = new[] { "a", "a(b(c))", "a(b,c)", "a,b,c", "a(b(d,e,f),c(g,h,i))" };

      foreach (var tree in trees)
      {
        var lazyTree = Tree.Lazy(() => TreeSerializer.DeserializeDepthFirstTree(tree));
        var direct = TreeSerializer.DeserializeDepthFirstTree(tree);

        CollectionAssert.AreEqual(
          direct.PreorderTraversal().ToArray(),
          lazyTree.PreorderTraversal().ToArray(),
          $"Preorder mismatch for {tree}");

        CollectionAssert.AreEqual(
          direct.LevelOrderTraversal().ToArray(),
          lazyTree.LevelOrderTraversal().ToArray(),
          $"LevelOrder mismatch for {tree}");
      }
    }

    [TestMethod]
    public void NarrowDualsPinOnce()
    {
      var depthFirstInvocations = 0;
      var breadthFirstInvocations = 0;

      var lazyDepthFirstTree = Tree.LazyDepthFirst(() =>
      {
        depthFirstInvocations++;
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      var lazyBreadthFirstTree = Tree.LazyBreadthFirst(() =>
      {
        breadthFirstInvocations++;
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      lazyDepthFirstTree.PreorderTraversal().ToArray();
      lazyDepthFirstTree.PreorderTraversal().ToArray();
      lazyBreadthFirstTree.LevelOrderTraversal().ToArray();
      lazyBreadthFirstTree.LevelOrderTraversal().ToArray();

      Assert.AreEqual(1, depthFirstInvocations);
      Assert.AreEqual(1, breadthFirstInvocations);
    }
  }
}
