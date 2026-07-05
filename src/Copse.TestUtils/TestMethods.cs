using Copse.Core;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;

namespace Copse.TestUtils
{
  public static class TestMethods
  {
    public static void TraversalTest(
      string serializedTree,
      string testDescription,
      Func<ITreenumerable<string>, ITreenumerable<string>> operation,
      Func<NodeContext<string>, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      NodeVisit<string>[] expectedTraversal,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      ITreenumerable<string> treenumerable;

      treenumerable =
        TreeSerializer
        .DeserializeDepthFirstTree(serializedTree)
        .Select(visit => visit.Node);

      Debug.WriteLine("--------- Test Name ---------");
      Debug.WriteLine(serializedTree);
      Debug.WriteLine(testDescription);

      Debug.WriteLine($"{Environment.NewLine}---- Expected Values ----");
      NodeVisitsDebugWriter.WriteNodeVisitHeader();
      foreach (var nodeVisit in expectedTraversal)
        Debug.WriteLine(nodeVisit);

      if (operation != null)
        treenumerable = operation(treenumerable);

      var traversal = treenumerable.GetTraversal(treeTraversalStrategy, nodeTraversalStrategiesSelector);

      // Act
      Debug.WriteLine($"{Environment.NewLine}----- Actual Values -----");
      NodeVisitsDebugWriter.WriteNodeVisitHeader();
      var actualTraversal =
        traversal
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      var diff = NodeVisitDiffer.Diff(expectedTraversal, actualTraversal);

      Debug.WriteLine($"{Environment.NewLine}---- Diffed Values ----");
      foreach (var diffResult in diff)
        Debug.WriteLine(diffResult);

      // Assert
      CollectionAssert.AreEqual(expectedTraversal, actualTraversal);
    }
  }
}
