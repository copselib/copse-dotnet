using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // SCRATCH (2026-08-18, the hoisting probe -- delete or promote once the light-tier
  // storage question is ruled): the benchmark's Hoisted row appends a
  // semantically-identity Where to the spelled chain to force the DRIVER route (which
  // evaluates the composed selector once per scheduled node and republishes stored values
  // from its path frames). These pins verify the two claims the timing rests on: the route
  // really is the driver, and the output really is unchanged.
  [TestClass]
  public class HoistingProbeTests
  {
    private static ITreenumerable<string> Tree()
      => TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e(f(g),h))");

    private static ITreenumerable<string> Spelled()
      => Tree()
        .WithPosition()
        .PruneAfter(pair => pair.Position.Depth == 1)
        .Select(pair => pair.Node);

    private static ITreenumerable<string> Hoisted()
      => Tree()
        .WithPosition()
        .PruneAfter(pair => pair.Position.Depth == 1)
        .Select(pair => pair.Node)
        .Where(_ => true);

    [TestMethod]
    public void TheHoistedSpelling_TakesTheDriverRoute()
    {
      Assert.AreEqual(
        typeof(SelectPruneAfterTreenumerable<,,>),
        Spelled().GetType().GetGenericTypeDefinition(),
        "the light route: the passthrough recomputes per visit");

      Assert.AreEqual(
        typeof(SelectWhereTreenumerable<,,>),
        Hoisted().GetType().GetGenericTypeDefinition(),
        "the hoisted route: ONE driver, storing values in its path frames");
    }

    [TestMethod]
    public void TheIdentityWhere_ChangesNothing()
    {
      var overload = Tree().PruneAfter((node, position) => position.Depth == 1);

      CollectionAssert.AreEqual(
        overload.GetPreorderTraversal().ToArray(),
        Hoisted().GetPreorderTraversal().ToArray(),
        "depth-first values");
      CollectionAssert.AreEqual(
        overload.GetLevelOrderTraversal().ToArray(),
        Hoisted().GetLevelOrderTraversal().ToArray(),
        "breadth-first values");

      // Positions too: an always-true Where removes nothing, so nothing is promoted and
      // no label moves.
      CollectionAssert.AreEqual(
        overload.WithPosition().GetPreorderTraversal()
          .Select(pair => $"{pair.Node}@{pair.Position.Depth}.{pair.Position.SiblingIndex}").ToArray(),
        Hoisted().WithPosition().GetPreorderTraversal()
          .Select(pair => $"{pair.Node}@{pair.Position.Depth}.{pair.Position.SiblingIndex}").ToArray(),
        "labels");
    }
  }
}
