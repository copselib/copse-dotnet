using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Stage A of design-docs/WITHPOSITION_DESIGN.md: WithPosition is the ONE positional
  // operator -- a projection into the NodeContext pair that the value algebra composes
  // over. These pins anchor its three laws from birth: it rides the light Select
  // machinery (representation), the pair carries its input's EMITTED labels (the join
  // rule as data flow), and captured pairs are data, not coordinates (capture scope is
  // explicit -- a relabeling operator downstream does not update them).
  [TestClass]
  public class WithPositionTests
  {
    private static ITreenumerable<string> Plain()
      => TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e)");

    private static string[] Describe(ITreenumerable<NodeContext<string>> pairs)
      => pairs.GetPreorderTraversal()
        .Select(pair => $"{pair.Node}@{pair.Position.Depth}.{pair.Position.SiblingIndex}")
        .ToArray();

    // ---- Representation: the positional projection's exact machines ----

    [TestMethod]
    public void Representation_RidesTheLightSelectMachinery()
    {
      // Plain source: the light wrapper.
      Assert.AreEqual(
        typeof(SelectTreenumerable<,>),
        Plain().WithPosition().GetType().GetGenericTypeDefinition(),
        "WithPosition over a plain source is the light Select wrapper");

      // Over a light chain: in-tier merge, still ONE light wrapper.
      Assert.AreEqual(
        typeof(SelectTreenumerable<,>),
        Plain().Select(n => n + "!").WithPosition().GetType().GetGenericTypeDefinition(),
        "WithPosition merges in-tier over a light chain");

      // Over a relabeling driver: stacks (the join rule -- the pair must carry EMITTED
      // labels, which a splice into the relabeling chain could not provide).
      Assert.AreEqual(
        typeof(SelectTreenumerable<,>),
        Plain().Where(n => n != "b").WithPosition().GetType().GetGenericTypeDefinition(),
        "WithPosition stacks over a relabeling chain");

      // The full sugar spelling collapses to ONE middle-tier wrapper (the erasure
      // argument: the pair is a stack-transient inside the composed selector).
      Assert.AreEqual(
        typeof(SelectPruneAfterTreenumerable<,,>),
        Plain().WithPosition().PruneAfter(nc => nc.Position.Depth == 1).Select(nc => nc.Node)
          .GetType().GetGenericTypeDefinition(),
        "the positional-prune spelling collapses to one wrapper");
    }

    // ---- The join rule as data flow: the pair carries its input's emitted labels ----

    [TestMethod]
    public void Pairs_CarryTheInputTreesEmittedLabels()
    {
      CollectionAssert.AreEqual(
        new[] { "a@0.0", "b@1.0", "c@2.0", "d@2.1", "e@1.1" },
        Describe(Plain().WithPosition()),
        "source labels");

      // After a relabeling operator: the pair reads the EMITTED labels (c and d promoted
      // to depth 1, e renumbered) -- what the join rule guarantees for positional
      // lambdas, here true by construction.
      CollectionAssert.AreEqual(
        new[] { "a@0.0", "c@1.0", "d@1.1", "e@1.2" },
        Describe(Plain().Where(n => n != "b").WithPosition()),
        "emitted labels after Where");
    }

    // ---- Capture scope: pairs are data, not coordinates ----

    [TestMethod]
    public void Pairs_AreDataNotCoordinates()
    {
      // Capture FIRST, filter SECOND: the emitted tree relabels (c, d promote), but the
      // captured pairs keep their capture-time coordinates -- the old capture is
      // deliberately stale, and the spelling says so. (This reading is not expressible
      // through the positional overloads at all.)
      CollectionAssert.AreEqual(
        new[] { "a@0.0", "c@2.0", "d@2.1", "e@1.1" },
        Describe(Plain().WithPosition().Where(pair => pair.Node != "b")),
        "captured pairs survive the relabel unchanged");
    }

    // ---- The sugar contract: the spelling is extensionally the positional overload ----

    [TestMethod]
    public void SugarEquivalence_TheSpellingMatchesThePositionalOverload()
    {
      var spelled = Plain()
        .WithPosition()
        .PruneAfter(pair => pair.Position.Depth == 1)
        .Select(pair => pair.Node);

      var overload = Plain().PruneAfter((node, position) => position.Depth == 1);

      CollectionAssert.AreEqual(
        overload.GetPreorderTraversal().ToArray(),
        spelled.GetPreorderTraversal().ToArray(),
        "depth-first");
      CollectionAssert.AreEqual(
        overload.GetLevelOrderTraversal().ToArray(),
        spelled.GetLevelOrderTraversal().ToArray(),
        "breadth-first");
    }
  }
}
