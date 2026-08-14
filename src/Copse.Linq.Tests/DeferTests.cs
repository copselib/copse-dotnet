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

      deferred.GetPreorderTraversal().ToArray();

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

      deferred.GetPreorderTraversal().ToArray();
      deferred.GetPreorderTraversal().ToArray();
      deferred.GetLevelOrderTraversal().ToArray();

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
          direct.GetPreorderTraversal().ToArray(),
          deferred.GetPreorderTraversal().ToArray(),
          $"Preorder mismatch for {tree}");

        CollectionAssert.AreEqual(
          direct.GetLevelOrderTraversal().ToArray(),
          deferred.GetLevelOrderTraversal().ToArray(),
          $"LevelOrder mismatch for {tree}");
      }
    }
  }
}
