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
          nodeContext => nodeContext.Node,
          (nodeContext, children) => $"{nodeContext.Node}{string.Join("", children)}")
        .ToArray();

      CollectionAssert.AreEqual(expected, actual);
    }
  }
}
