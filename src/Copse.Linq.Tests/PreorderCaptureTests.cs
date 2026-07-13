using Copse.SimpleSerializer;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // PreorderCapture: the flat family's shared encode (the shape-A capture loop hoisted out of
  // the operator builds). A capture must round-trip any source exactly through the preorder
  // decoders, and the side channel must be preorder-parallel, evaluated against SOURCE contexts.
  [TestClass]
  public class PreorderCaptureTests
  {
    private static readonly string[] Trees =
    {
      "",
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a,b(c)",
      "a(b(d(e)),c)",
      "a(b(d,e,f),c(g,h,i))",
      "a(d(g)),b(e(h)),c(f(i))",
    };

    [TestMethod]
    public void Captures_round_trip_through_the_preorder_decoders()
    {
      foreach (var tree in Trees)
      {
        var capture = PreorderCapture.CaptureFrom(TreeSerializer.DeserializeDepthFirstTree(tree));

        var replay = new PreorderTreenumerable<string, PreorderArrayStore<string>>(capture);

        Assert.AreEqual(tree, replay.SerializeDepthFirstTree(), $"round-trip mismatch for '{tree}'");
      }
    }

    [TestMethod]
    public void Side_channel_is_preorder_parallel_and_sees_the_source_context()
    {
      var source = TreeSerializer.DeserializeDepthFirstTree("a(b(d),c)");

      var (store, sideChannel) = PreorderCapture.CaptureFrom(
        source,
        nodeContext => $"{nodeContext.Node}@{nodeContext.Position.Depth}");

      CollectionAssert.AreEqual(new[] { "a@0", "b@1", "d@2", "c@1" }, sideChannel);
      CollectionAssert.AreEqual(
        new[] { "a", "b", "d", "c" },
        Enumerable.Range(0, store.Count).Select(store.GetValue).ToArray());
    }
  }
}
