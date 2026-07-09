using Copse.Core;
using Copse.Core.Async;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Validates the ASYNC forward-only deserializer (async scanner + async text streams + async stream
  // drivers, all reached through TreeSerializer.Deserialize*Async) by driving it over an in-memory
  // reader and asserting its full visit stream equals the SYNC deserializer's -- whose generated sync
  // twins are themselves conformance-checked against the engine oracle (FlatFamilyStreamConformance).
  // So async == sync == oracle, including the scanner's quote/pushback/trim paths.
  [TestClass]
  public class AsyncDeserializeTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(d,e),c)",
      "a,b,c",
      "a,b(c)",
      "a(b,c,d)",
      "a(b(d(e)),c)",
      "a(b(d,e,f),c(g,h,i))",
      "\"quoted (with parens, and ; too)\"(child)",
    };

    [TestMethod]
    public async Task AsyncDeserialize_DepthFirst_MatchesSyncDeserialize()
    {
      foreach (var tree in Trees)
      {
        var expected = Collect(
          TreeSerializer.DeserializeDepthFirstTree(() => new StringReader(tree)).GetDepthFirstTreenumerator());

        var actual = await CollectAsync(
          TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(tree)).GetAsyncDepthFirstTreenumerator());

        CollectionAssert.AreEqual(expected, actual, $"DFT async deserialize mismatch for '{tree}'");
      }
    }

    [TestMethod]
    public async Task AsyncDeserialize_BreadthFirst_MatchesSyncDeserialize()
    {
      foreach (var tree in Trees)
      {
        // Re-serialize breadth-first so the input is a valid level-order string by construction.
        var bft = TreeSerializer.DeserializeDepthFirstTree(tree).SerializeBreadthFirstTree();

        var expected = Collect(
          TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader(bft)).GetBreadthFirstTreenumerator());

        var actual = await CollectAsync(
          TreeSerializer.DeserializeBreadthFirstTreeAsync(() => new StringReader(bft)).GetAsyncBreadthFirstTreenumerator());

        CollectionAssert.AreEqual(expected, actual, $"BFT async deserialize mismatch for '{bft}' (from '{tree}')");
      }
    }

    private static List<(TreenumeratorMode, string, int, int, int)> Collect(ITreenumerator<string> t)
    {
      var visits = new List<(TreenumeratorMode, string, int, int, int)>();
      using (t)
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          visits.Add((t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
      return visits;
    }

    private static async Task<List<(TreenumeratorMode, string, int, int, int)>> CollectAsync(IAsyncTreenumerator<string> t)
    {
      var visits = new List<(TreenumeratorMode, string, int, int, int)>();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          visits.Add((t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
      return visits;
    }
  }
}
