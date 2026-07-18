using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The narrow (single-dimension) halves of the composition surface: chains over a
  // depth-first-only or breadth-first-only source must collapse exactly like composite-width
  // chains -- same wrapper lattice, same tiering, same join rule -- while staying statically
  // narrow at every link. The force-stacked controls: Tree.DeferDepthFirst/DeferBreadthFirst's
  // delegating wrappers are not composable, so inserting one breaks any chain without changing
  // semantics.
  [TestClass]
  public class NarrowCompositionTests
  {
    private static IDepthFirstTreenumerable<string> StreamDepthFirst(string tree)
    {
      var envelope = TreeSerializer.DeserializeDepthFirstTree(tree).SerializeDepthFirstTree();
      return TreeSerializer.DeserializeDepthFirstTree(() => new StringReader(envelope));
    }

    private static IBreadthFirstTreenumerable<string> StreamBreadthFirst(string tree)
    {
      var envelope = TreeSerializer.DeserializeDepthFirstTree(tree).SerializeBreadthFirstTree();
      return TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader(envelope));
    }

    [TestMethod]
    public void NarrowValueChains_CollapseToOneWrapper_AnyOrder()
    {
      // The narrow twin of the composite closure pin: any order, any length, one wrapper.
      IDepthFirstTreenumerable<string> depthFirst = StreamDepthFirst("a(b,c)")
        .Where(n => n != "b")
        .Select(n => n + "!")
        .Where(n => n != "c!")
        .Select(n => n + "?");

      Assert.IsInstanceOfType(
        depthFirst,
        typeof(SelectWhereDepthFirstTreenumerable<string, string, FuncResultSelector<string, string>>));

      IBreadthFirstTreenumerable<string> breadthFirst = StreamBreadthFirst("a(b,c)")
        .Where(n => n != "b")
        .Select(n => n + "!")
        .Where(n => n != "c!")
        .Select(n => n + "?");

      Assert.IsInstanceOfType(
        breadthFirst,
        typeof(SelectWhereBreadthFirstTreenumerable<string, string, FuncResultSelector<string, string>>));
    }

    [TestMethod]
    public void NarrowValueWheres_Compose_AndMatchTheStackedPipeline()
    {
      var composedDepthFirst = StreamDepthFirst("a(b(d,e,f),c)")
        .Where(n => n != "b")
        .Where(n => n != "e")
        .PreorderTraversal().ToArray();

      var stackedDepthFirst = Tree.DeferDepthFirst(() => StreamDepthFirst("a(b(d,e,f),c)").Where(n => n != "b"))
        .Where(n => n != "e")
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(stackedDepthFirst, composedDepthFirst, "depth-first");

      var composedBreadthFirst = StreamBreadthFirst("a(b(d,e,f),c)")
        .Where(n => n != "b")
        .Where(n => n != "e")
        .LevelOrderTraversal().ToArray();

      var stackedBreadthFirst = Tree.DeferBreadthFirst(() => StreamBreadthFirst("a(b(d,e,f),c)").Where(n => n != "b"))
        .Where(n => n != "e")
        .LevelOrderTraversal().ToArray();

      CollectionAssert.AreEqual(stackedBreadthFirst, composedBreadthFirst, "breadth-first");
    }

    [TestMethod]
    public void NarrowSelects_StayOnTheLightWrapper()
    {
      IDepthFirstTreenumerable<string> depthFirst = StreamDepthFirst("a(b,c)")
        .Select(n => n + "1")
        .Select(n => n + "2");

      Assert.IsInstanceOfType(depthFirst, typeof(SelectDepthFirstTreenumerable<string, string>));

      IBreadthFirstTreenumerable<string> breadthFirst = StreamBreadthFirst("a(b,c)")
        .Select(n => n + "1")
        .Select(n => n + "2");

      Assert.IsInstanceOfType(breadthFirst, typeof(SelectBreadthFirstTreenumerable<string, string>));

      CollectionAssert.AreEqual(
        new[] { "a12", "b12", "c12" },
        depthFirst.PreorderTraversal().ToArray());
    }

    [TestMethod]
    public void NarrowSelectThenPruneAfter_StaysOnTheLightTier()
    {
      IDepthFirstTreenumerable<string> composed = StreamDepthFirst("a(b(d,e),c)")
        .Select(n => n + "!")
        .PruneAfter(n => n == "b!");

      Assert.IsInstanceOfType(composed, typeof(SelectPruneAfterDepthFirstTreenumerable<string, string>));

      var stacked = Tree.DeferDepthFirst(() => StreamDepthFirst("a(b(d,e),c)").Select(n => n + "!"))
        .PruneAfter(n => n == "b!")
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(stacked, composed.PreorderTraversal().ToArray());
    }

    [TestMethod]
    public void NarrowPruneAfterOverPruneAfter_StaysOnTheBespokeDriver()
    {
      IDepthFirstTreenumerable<string> depthFirst = StreamDepthFirst("a(b(d),c(e))")
        .PruneAfter(n => n == "b")
        .PruneAfter(n => n == "c");

      Assert.IsInstanceOfType(depthFirst, typeof(PruneAfterDepthFirstTreenumerable<string>));

      IBreadthFirstTreenumerable<string> breadthFirst = StreamBreadthFirst("a(b(d),c(e))")
        .PruneAfter(n => n == "b")
        .PruneAfter(n => n == "c");

      Assert.IsInstanceOfType(breadthFirst, typeof(PruneAfterBreadthFirstTreenumerable<string>));

      CollectionAssert.AreEqual(
        new[] { "a", "b", "c" },
        depthFirst.PreorderTraversal().ToArray());
    }

    [TestMethod]
    public void NarrowLightTier_ConvertsWhenARejectingOperatorJoins()
    {
      IDepthFirstTreenumerable<string> converted = StreamDepthFirst("a(b(d,e),c)")
        .Select(n => n + "!")
        .PruneAfter(n => n == "b!")
        .Where(n => n != "c!");

      Assert.IsInstanceOfType(
        converted,
        typeof(SelectWhereDepthFirstTreenumerable<string, string, FuncResultSelector<string, string>>),
        "a rejecting operator must convert the narrow light tier to the general representation");
    }

    // The join rule, narrow half: a positional lambda is entitled to its input tree's emitted
    // labels, so after a relabeling operator it stacks a real layer and sees the relabeled
    // coordinates.
    [TestMethod]
    public void NarrowPositionalSelect_AfterWhere_SeesTheEmittedLabels()
    {
      var labeled = StreamDepthFirst("a(b(c))")
        .Where(n => n != "b")
        .Select((n, position) => $"{n}@{position.Depth}")
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { "a@0", "c@1" }, labeled);
    }

    // PruneAfter is label-preserving, so the narrow positional Select composes across it and
    // the chain stays on the light tier.
    [TestMethod]
    public void NarrowPositionalSelect_ComposesAcrossPruneAfter()
    {
      IDepthFirstTreenumerable<string> composed = StreamDepthFirst("a(b(c),d)")
        .PruneAfter(n => n == "b")
        .Select((n, position) => $"{n}@{position.Depth}.{position.SiblingIndex}");

      Assert.IsInstanceOfType(composed, typeof(SelectPruneAfterDepthFirstTreenumerable<string, string>));

      CollectionAssert.AreEqual(
        new[] { "a@0.0", "b@1.0", "d@1.1" },
        composed.PreorderTraversal().ToArray());
    }

    // A composite-width chain continued through a narrow-typed receiver: the narrow overload
    // probes the composite recipe surface first, so the chain keeps composing on its own
    // representation (and the successor keeps both dimensions under the narrow static type).
    [TestMethod]
    public void CompositeChain_ContinuedThroughANarrowReceiver_KeepsComposing()
    {
      IDepthFirstTreenumerable<string> narrowed =
        TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Where(n => n != "b");

      IDepthFirstTreenumerable<string> continued = narrowed.Where(n => n != "e");

      Assert.IsInstanceOfType(
        continued,
        typeof(SelectWhereTreenumerable<string, string, FuncResultSelector<string, string>>),
        "the composite wrapper must keep composing, not stack a narrow layer");

      CollectionAssert.AreEqual(
        new[] { "a", "d", "c" },
        continued.PreorderTraversal().ToArray());
    }

    // Narrow composed chains against the engine oracle, over the conformance corpus: the same
    // operator chain over a full-citizen engine tree must produce the identical visit stream.
    [TestMethod]
    public void NarrowComposedChains_ConformToTheEngine()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        IDepthFirstTreenumerable<string> narrowDepthFirst = StreamDepthFirst(tree)
          .Select(n => n.ToUpperInvariant())
          .Where(n => n != "B")
          .PruneBefore(n => n == "D")
          .PruneAfter(n => n == "C");

        ITreenumerable<string> engine = TreeSerializer.DeserializeDepthFirstTree(tree)
          .Select(n => n.ToUpperInvariant())
          .Where(n => n != "B")
          .PruneBefore(n => n == "D")
          .PruneAfter(n => n == "C");

        VisitStreamConformance.AssertSameStream(
          engine.GetDepthFirstTreenumerator(),
          narrowDepthFirst.GetDepthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"narrow composed DFT chain {tree}");

        IBreadthFirstTreenumerable<string> narrowBreadthFirst = StreamBreadthFirst(tree)
          .Select(n => n.ToUpperInvariant())
          .Where(n => n != "B")
          .PruneBefore(n => n == "D")
          .PruneAfter(n => n == "C");

        VisitStreamConformance.AssertSameStream(
          engine.GetBreadthFirstTreenumerator(),
          narrowBreadthFirst.GetBreadthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"narrow composed BFT chain {tree}");
      }
    }
  }
}
