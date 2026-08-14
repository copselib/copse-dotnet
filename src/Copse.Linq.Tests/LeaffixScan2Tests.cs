using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The experiment's conformance battery: LeaffixScan2 (the walker-tier reimplementation)
  // against LeaffixScan (the incumbent, the oracle) -- identical visit streams, both
  // dimensions, on an order-SENSITIVE scan so sibling reduction order is pinned, over the
  // shape corpus. If these ever disagree, the experiment failed, not the incumbent.
  [TestClass]
  public class LeaffixScan2Tests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b)",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a(b(d(h,i),e),c(f,g(j)))",
      "a(b(c(d(e(f(g))))))",
      "a(b,c,d,e,f)",
    };

    // Order-sensitive on both callbacks: the edge fold's left-association and the node
    // fold's nesting are both visible in the strings.
    private static string Edge(string left, string right) => $"{left},{right}";
    private static string Node(string accumulate, string node) => $"{node}[{accumulate}]";

    [TestMethod]
    public void LeaffixScan2_MatchesTheIncumbent_BothDimensions()
    {
      foreach (var tree in Trees)
      {
        var walkable = TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.Preorder);

        var incumbent = walkable.LeaffixScan("~", Edge, Node);
        var reimplementation = walkable.LeaffixScan2("~", Edge, Node);

        foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        {
          CollectionAssert.AreEqual(
            incumbent.GetTraversal(strategy).Select(Describe).ToList(),
            reimplementation.GetTraversal(strategy).Select(Describe).ToList(),
            $"{tree} ({strategy})");
        }
      }
    }

    private static string Describe(NodeVisit<ScanResult<string, string>> visit)
      => $"{visit.Mode} {visit.Node.Node}<-{visit.Node.Accumulate} @{visit.Position} x{visit.VisitCount}";
  }
}
