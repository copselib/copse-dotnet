using Copse.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class EmptyTests
  {

    [TestMethod]
    public void BreadthFirst_GetStateBeforeMoveNext()
    {
      // Arrange
      var treenumerator = Treenumerable.Empty<int>().GetBreadthFirstTreenumerator();

      // Act

      // Assert
      Assert.AreEqual(NodePosition.ForestRoot, treenumerator.Position);
      Assert.AreEqual(default, treenumerator.Mode);
      Assert.AreEqual(0, treenumerator.VisitCount);
      Assert.AreEqual(0, treenumerator.Node);
    }

    [TestMethod]
    public void BreadthFirst_Traverse()
    {
      // Arrange
      var treenumerable = Treenumerable.Empty<int>();

      var expected = Array.Empty<NodeVisit<int>>();

      // Act
      var actual =
        treenumerable
        .GetBreadthFirstTraversal()
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void BreadthFirst_MoveNextReturnsFalse()
    {
      // Arrange
      var treenumerator = Treenumerable.Empty<int>().GetBreadthFirstTreenumerator();

      // Act
      var actual = treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

      // Assert
      Assert.AreEqual(false, actual);
    }

    [TestMethod]
    public void DepthFirst_GetStateBeforeMoveNext()
    {
      // Arrange
      var treenumerator = Treenumerable.Empty<int>().GetDepthFirstTreenumerator();

      // Act

      // Assert
      Assert.AreEqual(NodePosition.ForestRoot, treenumerator.Position);
      Assert.AreEqual(default, treenumerator.Mode);
      Assert.AreEqual(0, treenumerator.VisitCount);
      Assert.AreEqual(0, treenumerator.Node);
    }

    [TestMethod]
    public void DepthFirst_Traverse()
    {
      // Arrange
      var treenumerable = Treenumerable.Empty<int>();

      var expected = Array.Empty<NodeVisit<int>>();

      // Act
      var actual =
        treenumerable
        .GetDepthFirstTraversal()
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DepthFirst_MoveNextReturnsFalse()
    {
      // Arrange
      var treenumerator = Treenumerable.Empty<int>().GetDepthFirstTreenumerator();

      // Act
      var actual = treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

      // Assert
      Assert.AreEqual(false, actual);
    }
  }
}
