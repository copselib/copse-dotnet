using Copse.SimpleSerializer;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Linq.Tests
{
  // LevelOrderCapture: the level-order dual of PreorderCapture (shape B, the memo buffer's
  // front-cursor parse in one-shot form). A capture must round-trip any source exactly through
  // the level-order decoders.
  [TestClass]
  public class LevelOrderCaptureTests
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
    public void Captures_round_trip_through_the_level_order_decoders()
    {
      foreach (var tree in Trees)
      {
        var capture = LevelOrderCapture.CaptureFrom(TreeSerializer.DeserializeDepthFirstTree(tree));

        var replay = new LevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(capture);

        Assert.AreEqual(tree, replay.SerializeDepthFirstTree(), $"round-trip mismatch for '{tree}'");
      }
    }
  }
}
