using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS check for the async bind (the semantics are pinned once, on the generated
  // sync twin, by SelectManyOperatorTests against the reference model): the async spelling
  // must run the four special values and a forest placement end to end over a genuinely
  // async source, in both dimensions -- the breadth-first one through its documented capture.
  [TestClass]
  public class AsyncSelectManyTests
  {
    private static IAsyncTreenumerable<string> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(tree)).Memoize();

    private static async Task<List<string>> DrainAsync(IAsyncTreenumerator<string> treenumerator)
    {
      var events = new List<string>();

      await using (treenumerator)
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll))
          events.Add($"{(treenumerator.Mode == TreenumeratorMode.SchedulingNode ? "S" : "V")}{treenumerator.VisitCount} {treenumerator.Node}@{treenumerator.Position.Depth}.{treenumerator.Position.SiblingIndex}");

      return events;
    }

    [TestMethod]
    public async Task TheQuartetAndAForest_BothDimensions()
    {
      // a(b(d,e),c): b promoted, d dropped, e leafed, c replaced by a two-root forest with the
      // slot after its roots, a returned -- expected a'(e!, c1, c2(c3)) per the model.
      var result = W("a(b(d,e),c)").SelectMany(value =>
        value == "b" ? AsyncExpansion.Promote<string>()
        : value == "d" ? AsyncExpansion.Drop<string>()
        : value == "e" ? AsyncExpansion.Leaf(value + "!")
        : value == "c" ? AsyncExpansion.Of(W("c1,c2(c3)"), SlotPlacement.AfterRoots)
        : AsyncExpansion.Return(value + "'"));

      var expected = W("a'(e!,c1,c2(c3))");

      CollectionAssert.AreEqual(
        await DrainAsync(expected.GetAsyncDepthFirstTreenumerator()),
        await DrainAsync(result.GetAsyncDepthFirstTreenumerator()),
        "depth-first");

      CollectionAssert.AreEqual(
        await DrainAsync(expected.GetAsyncBreadthFirstTreenumerator()),
        await DrainAsync(result.GetAsyncBreadthFirstTreenumerator()),
        "breadth-first (the documented capture)");
    }
  }
}
