using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The receiver-smart operators' cross-path conformance (the 2026-08-14 experiment's
  // collapse): LeaffixScan and Invert dispatch by what their receiver affords -- the
  // streaming engine for streams and level-order captures, the span fast path for the
  // concrete preorder buffer, the walker-probe fold for a foreign walkable (a memo). Every
  // path must produce the ENGINE's exact result: the stream path is the oracle, and the
  // Where-wrap keeps a source stream-shaped so the sniff cannot fire on the oracle side.
  [TestClass]
  public class ReceiverSmartOperatorTests
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

    // Order-sensitive on both callbacks so sibling reduction order is pinned.
    private static string Edge(string left, string right) => $"{left},{right}";
    private static string Node(string accumulate, string node) => $"{node}[{accumulate}]";

    [TestMethod]
    public void LeaffixScan_EveryReceiverShape_MatchesTheEngine()
    {
      foreach (var tree in Trees)
      {
        var stream = TreeSerializer.DeserializeDepthFirstTree(tree);

        var oracle = stream.Where(context => true).LeaffixScan(leaf => Node("~", leaf), Edge, Node)
          .GetTraversal(TreeTraversalStrategy.DepthFirst).Select(DescribeScan).ToList();

        AssertScan(oracle, stream.Materialize(BufferLayout.Preorder).LeaffixScan(leaf => Node("~", leaf), Edge, Node), $"{tree} (preorder buffer, span path)");
        AssertScan(oracle, stream.Materialize(BufferLayout.LevelOrder).LeaffixScan(leaf => Node("~", leaf), Edge, Node), $"{tree} (level-order buffer, engine path)");

        using var memo = stream.Memoize();
        AssertScan(oracle, memo.LeaffixScan(leaf => Node("~", leaf), Edge, Node), $"{tree} (memo, walker path)");
      }
    }

    [TestMethod]
    public void Invert_EveryReceiverShape_MatchesTheEngine()
    {
      foreach (var tree in Trees)
      {
        var stream = TreeSerializer.DeserializeDepthFirstTree(tree);

        foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        {
          var oracle = stream.Where(context => true).Invert().GetTraversal(strategy).Select(DescribeNode).ToList();

          CollectionAssert.AreEqual(
            oracle,
            stream.Materialize(BufferLayout.Preorder).Invert().GetTraversal(strategy).Select(DescribeNode).ToList(),
            $"{tree} ({strategy}, preorder buffer, span path)");

          CollectionAssert.AreEqual(
            oracle,
            stream.Materialize(BufferLayout.LevelOrder).Invert().GetTraversal(strategy).Select(DescribeNode).ToList(),
            $"{tree} ({strategy}, level-order buffer, engine path)");

          using var memo = stream.Memoize();
          CollectionAssert.AreEqual(
            oracle,
            memo.Invert().GetTraversal(strategy).Select(DescribeNode).ToList(),
            $"{tree} ({strategy}, memo, walker path)");
        }
      }
    }

    // The in-place result buffer carries probes at birth (sharing the fold's own store) --
    // adjacency works on it without any settle, and the handles are the RESULT's preorder
    // ordinals.
    [TestMethod]
    public void LeaffixScan_InPlaceResult_IsWalkableAtBirth()
    {
      var buffer = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Materialize(BufferLayout.Preorder);

      var scan = buffer.LeaffixScan(node => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1);

      Assert.AreEqual(5, WalkerLawProviders.TopologyOf(scan).GetValue(0).Accumulate, "the root's accumulate is the whole tree's count");
      Assert.AreEqual("a", WalkerLawProviders.TopologyOf(scan).GetValue(0).Node);
      Assert.IsTrue(WalkerLawProviders.TopologyOf(scan).TryGetChildAt(0, 0).HasChild);
    }

    private static void AssertScan(
      System.Collections.Generic.List<string> oracle,
      ITreenumerableBuffer<NodeAccumulation<string, string>> actual,
      string label)
      => CollectionAssert.AreEqual(
        oracle,
        actual.GetTraversal(TreeTraversalStrategy.DepthFirst).Select(DescribeScan).ToList(),
        label);

    private static string DescribeScan(NodeVisit<NodeAccumulation<string, string>> visit)
      => $"{visit.Mode} {visit.Node.Node}<-{visit.Node.Accumulate} @{visit.Position} x{visit.VisitCount}";

    private static string DescribeNode(NodeVisit<string> visit)
      => $"{visit.Mode} {visit.Node} @{visit.Position} x{visit.VisitCount}";
  }
}
