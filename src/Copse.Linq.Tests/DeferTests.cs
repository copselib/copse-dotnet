using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class DeferTests
  {
    [TestMethod]
    public void FactoryNotInvokedUntilEnumeration()
    {
      var invocations = 0;

      var deferred = Tree.Defer(() =>
      {
        invocations++;
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      Assert.AreEqual(0, invocations);

      deferred.PreorderTraversal().ToArray();

      Assert.AreEqual(1, invocations);
    }

    [TestMethod]
    public void FactoryInvokedPerTreenumeratorAcquisition()
    {
      var invocations = 0;

      var deferred = Tree.Defer(() =>
      {
        invocations++;
        return TreeSerializer.DeserializeDepthFirstTree("a(b,c)");
      });

      deferred.PreorderTraversal().ToArray();
      deferred.PreorderTraversal().ToArray();
      deferred.LevelOrderTraversal().ToArray();

      Assert.AreEqual(3, invocations);
    }

    [TestMethod]
    public void TraversalsMatchTheInnerTree()
    {
      var trees = new[] { "a", "a(b(c))", "a(b,c)", "a,b,c", "a(b(d,e,f),c(g,h,i))" };

      foreach (var tree in trees)
      {
        var deferred = Tree.Defer(() => TreeSerializer.DeserializeDepthFirstTree(tree));
        var direct = TreeSerializer.DeserializeDepthFirstTree(tree);

        CollectionAssert.AreEqual(
          direct.PreorderTraversal().ToArray(),
          deferred.PreorderTraversal().ToArray(),
          $"Preorder mismatch for {tree}");

        CollectionAssert.AreEqual(
          direct.LevelOrderTraversal().ToArray(),
          deferred.LevelOrderTraversal().ToArray(),
          $"LevelOrder mismatch for {tree}");
      }
    }
  }
}
