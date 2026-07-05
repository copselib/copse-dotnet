using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class SelectTests
  {
    [TestMethod]
    public void PreorderTraversal_TwoLevels()
    {
      // Arrange
      var treenumerable =
        TreeSerializer
        .DeserializeDepthFirstTree("1,2,3", int.Parse);

      // Act
      var actual =
        treenumerable
        .Select(visit => (char)('a' + visit.Node))
        .PreorderTraversal()
        .ToArray();

      // Assert
      var expected = new[] { 'b', 'c', 'd' };

      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void LevelOrderTraversal_TwoLevels()
    {
      // Arrange
      var treenumerable =
        TreeSerializer
        .DeserializeDepthFirstTree("1,2,3", int.Parse);

      // Act
      var actual =
        treenumerable
        .Select(visit => (char)('a' + visit.Node))
        .LevelOrderTraversal()
        .ToArray();

      // Assert
      var expected = new[] { 'b', 'c', 'd' };

      CollectionAssert.AreEqual(expected, actual);
    }
  }
}
