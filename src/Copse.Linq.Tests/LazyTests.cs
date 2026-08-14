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

      lazyTree.GetPreorderTraversal().ToArray();

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

      lazyTree.GetPreorderTraversal().ToArray();
      lazyTree.GetPreorderTraversal().ToArray();
      lazyTree.GetLevelOrderTraversal().ToArray();

      Assert.AreEqual(1, invocations);
    }

    [TestMethod]
    public void ImpureFactoryStillYieldsOneTree()
    {
      // Under Defer each traversal would see a different tree; Lazy pins the first.
      var built = 0;

      var lazyTree = Tree.Lazy(
        () => TreeSerializer.DeserializeDepthFirstTree(built++ == 0 ? "a(b,c)" : "x"));

      var breadthFirst = lazyTree.GetLevelOrderTraversal().ToArray();
      var depthFirst = lazyTree.GetPreorderTraversal().ToArray();

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

      lazyTree.GetLevelOrderTraversal().ToArray();
      lazyTree.GetPreorderTraversal().ToArray();

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

      lazyTree.GetPreorderTraversal().ToArray();
      lazyTree.GetLevelOrderTraversal().ToArray();

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
          direct.GetPreorderTraversal().ToArray(),
          lazyTree.GetPreorderTraversal().ToArray(),
          $"Preorder mismatch for {tree}");

        CollectionAssert.AreEqual(
          direct.GetLevelOrderTraversal().ToArray(),
          lazyTree.GetLevelOrderTraversal().ToArray(),
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

      lazyDepthFirstTree.GetPreorderTraversal().ToArray();
      lazyDepthFirstTree.GetPreorderTraversal().ToArray();
      lazyBreadthFirstTree.GetLevelOrderTraversal().ToArray();
      lazyBreadthFirstTree.GetLevelOrderTraversal().ToArray();

      Assert.AreEqual(1, depthFirstInvocations);
      Assert.AreEqual(1, breadthFirstInvocations);
    }
  }
}
