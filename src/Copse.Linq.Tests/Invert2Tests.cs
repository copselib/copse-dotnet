using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The second experiment's conformance: Invert2 against the incumbent Invert as oracle --
  // identical visit streams, both dimensions, over the shape corpus (forests included: the
  // mirror reverses ROOTS too, and the oracle says so). The memo receiver pins the walker
  // fallback; the Materialize receiver pins the span fast path.
  [TestClass]
  public class Invert2Tests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b)",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b,c),d(e,f)",
      "a(b(d,e),c(f,g))",
      "a(b(d(h,i),e),c(f,g(j)))",
      "a(b(c(d(e(f(g))))))",
      "a(b,c,d,e,f)",
    };

    [TestMethod]
    public void Invert2_MatchesTheIncumbent_BothDimensions_BothPaths()
    {
      foreach (var tree in Trees)
      {
        var stream = TreeSerializer.DeserializeDepthFirstTree(tree);

        foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        {
          var oracle = stream.Invert().GetTraversal(strategy).Select(Describe).ToList();

          CollectionAssert.AreEqual(
            oracle,
            stream.Materialize(BufferLayout.Preorder).Invert2().GetTraversal(strategy).Select(Describe).ToList(),
            $"{tree} ({strategy}, span path)");

          using var memo = stream.Memoize();
          CollectionAssert.AreEqual(
            oracle,
            memo.Invert2().GetTraversal(strategy).Select(Describe).ToList(),
            $"{tree} ({strategy}, walker path)");
        }
      }
    }

    private static string Describe(NodeVisit<string> visit)
      => $"{visit.Mode} {visit.Node} @{visit.Position} x{visit.VisitCount}";
  }
}
