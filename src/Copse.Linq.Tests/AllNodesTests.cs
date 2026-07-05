using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Linq.Tests
{
  // Regression pin for the 2026-07-05 fix: AllNodes used to return AnyNodes(!predicate) with no
  // outer negation -- the complement of its name -- and nothing covered it.
  [TestClass]
  public class AllNodesTests
  {
    [TestMethod]
    public void AllNodesIsTrueWhenEveryNodeSatisfiesThePredicate()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)");

      Assert.IsTrue(tree.AllNodes(nodeContext => nodeContext.Node.Length == 1));
      Assert.IsTrue(tree.AllNodes(nodeContext => nodeContext.Node.Length == 1, TreeTraversalStrategy.BreadthFirst));
    }

    [TestMethod]
    public void AllNodesIsFalseWhenAnyNodeFailsThePredicate()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)");

      Assert.IsFalse(tree.AllNodes(nodeContext => nodeContext.Node != "d"));
      Assert.IsFalse(tree.AllNodes(nodeContext => nodeContext.Node != "d", TreeTraversalStrategy.BreadthFirst));
    }

    [TestMethod]
    public void AllNodesIsVacuouslyTrueOnTheEmptyForest()
    {
      Assert.IsTrue(TreeSerializer.DeserializeDepthFirstTree("").AllNodes(_ => false));
    }

    [TestMethod]
    public void NarrowOverloadsAgree()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)");

      Assert.IsTrue(((IDepthFirstTreenumerable<string>)tree).AllNodes(nodeContext => nodeContext.Node.Length == 1));
      Assert.IsFalse(((IDepthFirstTreenumerable<string>)tree).AllNodes(nodeContext => nodeContext.Node != "d"));
      Assert.IsTrue(((IBreadthFirstTreenumerable<string>)tree).AllNodes(nodeContext => nodeContext.Node.Length == 1));
      Assert.IsFalse(((IBreadthFirstTreenumerable<string>)tree).AllNodes(nodeContext => nodeContext.Node != "d"));
    }
  }
}
