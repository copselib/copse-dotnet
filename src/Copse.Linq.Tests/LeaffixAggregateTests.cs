using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class LeaffixAggregateTests
  {
    // Value flavor: nodeSelector projects each node to its own value and each child's
    // completed fold concatenates in sibling order, so a root's value is the concatenation of
    // its whole subtree. Expected roots are ';'-separated.
    [DataTestMethod]
    [DataRow("", "")]
    [DataRow("a", "a")]
    [DataRow("a(b(c,d))", "abcd")]
    [DataRow("a(b,c)", "abc")]
    [DataRow("a(b(d),c)", "abdc")]
    [DataRow("a,b,c", "a;b;c")]
    [DataRow("a(c),b(d)", "ac;bd")]
    [DataRow("a(c,d),b(e,f)", "acd;bef")]
    [DataRow("a(d),b,c(e)", "ad;b;ce")]
    public void AggregatesEachRootSubtree(string treeString, string expectedRoots)
    {
      var expected = expectedRoots.Length == 0 ? new string[0] : expectedRoots.Split(';');

      var actual =
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .LeaffixAggregate(
          nodeContext => nodeContext.Node,
          (accumulate, childAccumulate) => accumulate + childAccumulate)
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      CollectionAssert.AreEqual(expected, actual);
    }

    // The breadth-first entry (documented capture: Materialize, then the depth-first fold over
    // the capture's replay) must produce exactly what the explicit hoist produces, which is
    // exactly what the depth-first entry produces.
    [DataTestMethod]
    [DataRow("")]
    [DataRow("a")]
    [DataRow("a(b(c,d))")]
    [DataRow("a(b(d),c)")]
    [DataRow("a,b,c")]
    [DataRow("a(c,d),b(e,f)")]
    [DataRow("a(d),b,c(e)")]
    public void BreadthFirstEntryMatchesTheExplicitHoist(string treeString)
    {
      // Context flavor, position included, so the oracle also pins the CONTEXTS the fold
      // reconstructs from the capture's child spans, not just the values -- the accumulator's
      // context is the folding parent's, the selector's is the node's own.
      string Seed(NodeContext<string> nodeContext)
        => $"{nodeContext.Node}@{nodeContext.Position.SiblingIndex}.{nodeContext.Position.Depth}";

      string Accumulate(NodeContext<string> nodeContext, string accumulate, string childAccumulate)
        => $"{accumulate}[{Seed(nodeContext)}<-{childAccumulate}]";

      var tree = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var hoisted =
        ((IBreadthFirstTreenumerable<string>)tree)
        .Materialize()
        .LeaffixAggregate(Seed, Accumulate)
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      var direct =
        ((IBreadthFirstTreenumerable<string>)tree)
        .LeaffixAggregate(Seed, Accumulate)
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      CollectionAssert.AreEqual(hoisted, direct, $"breadth-first entry disagrees for '{treeString}'");
    }
  }
}
