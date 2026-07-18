using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class LeaffixAggregateTests
  {
    // Each node accumulates to its own value concatenated with its children's results, so a root's
    // value is the concatenation of its whole subtree. Expected roots are ';'-separated.
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
          (nodeContext, children) => $"{nodeContext.Node}{string.Join("", children)}",
          nodeContext => nodeContext.Node)
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
      // Position included so the oracle also pins the CONTEXTS the fold reconstructs
      // from the capture's child spans, not just the values.
      string Accumulate(NodeContext<string> nodeContext, ChildAccumulations<string> children)
        => $"{nodeContext.Node}@{nodeContext.Position.SiblingIndex}.{nodeContext.Position.Depth}({string.Join(",", children)})";

      var tree = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var hoisted =
        ((IBreadthFirstTreenumerable<string>)tree)
        .Materialize()
        .LeaffixAggregate(Accumulate, nodeContext => nodeContext.Node)
        .ToArray();

      var direct =
        ((IBreadthFirstTreenumerable<string>)tree)
        .LeaffixAggregate(Accumulate, nodeContext => nodeContext.Node)
        .ToArray();

      CollectionAssert.AreEqual(hoisted, direct, $"breadth-first entry disagrees for '{treeString}'");
    }
  }
}
