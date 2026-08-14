using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Part three's conformance: the receiver-smart LeaffixScan3 against the incumbent oracle
  // over every receiver shape the sniff can meet -- a plain stream (engine path), a
  // preorder capture (span fast path), a LEVEL-ORDER capture (must fall back to the
  // engine: its ordinals are not preorder), and a memo (walker-fold path).
  [TestClass]
  public class LeaffixScan3Tests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a(b(d(h,i),e),c(f,g(j)))",
      "a(b,c,d,e,f)",
    };

    private static string Edge(string left, string right) => $"{left},{right}";
    private static string Node(string accumulate, string node) => $"{node}[{accumulate}]";

    [TestMethod]
    public void LeaffixScan3_MatchesTheIncumbent_OverEveryReceiverShape()
    {
      foreach (var tree in Trees)
      {
        var stream = TreeSerializer.DeserializeDepthFirstTree(tree);

        var oracle = stream.LeaffixScan("~", Edge, Node)
          .GetTraversal(TreeTraversalStrategy.DepthFirst).Select(Describe).ToList();

        AssertMatches(oracle, stream.Where(context => true).LeaffixScan3("~", Edge, Node), $"{tree} (stream)");
        AssertMatches(oracle, stream.Materialize(BufferLayout.Preorder).LeaffixScan3("~", Edge, Node), $"{tree} (preorder buffer)");
        AssertMatches(oracle, stream.Materialize(BufferLayout.LevelOrder).LeaffixScan3("~", Edge, Node), $"{tree} (level-order buffer)");

        using var memo = stream.Memoize();
        AssertMatches(oracle, memo.LeaffixScan3("~", Edge, Node), $"{tree} (memo)");
      }
    }

    private static void AssertMatches(
      System.Collections.Generic.List<string> oracle,
      ITreenumerableBuffer<ScanResult<string, string>> actual,
      string label)
      => CollectionAssert.AreEqual(
        oracle,
        actual.GetTraversal(TreeTraversalStrategy.DepthFirst).Select(Describe).ToList(),
        label);

    private static string Describe(NodeVisit<ScanResult<string, string>> visit)
      => $"{visit.Mode} {visit.Node.Node}<-{visit.Node.Accumulate} @{visit.Position} x{visit.VisitCount}";
  }
}
