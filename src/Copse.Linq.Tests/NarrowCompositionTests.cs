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

      Assert.AreEqual(typeof(SelectWhereDepthFirstTreenumerable<,,>), depthFirst.GetType().GetGenericTypeDefinition());

      IBreadthFirstTreenumerable<string> breadthFirst = StreamBreadthFirst("a(b,c)")
        .Where(n => n != "b")
        .Select(n => n + "!")
        .Where(n => n != "c!")
        .Select(n => n + "?");

      Assert.AreEqual(typeof(SelectWhereBreadthFirstTreenumerable<,,>), breadthFirst.GetType().GetGenericTypeDefinition());
    }

    [TestMethod]
    public void NarrowValueWheres_Compose_AndMatchTheStackedPipeline()
    {
      var composedDepthFirst = StreamDepthFirst("a(b(d,e,f),c)")
        .Where(n => n != "b")
        .Where(n => n != "e")
        .GetPreorderTraversal().ToArray();

      var stackedDepthFirst = Tree.DeferDepthFirst(() => StreamDepthFirst("a(b(d,e,f),c)").Where(n => n != "b"))
        .Where(n => n != "e")
        .GetPreorderTraversal().ToArray();

      CollectionAssert.AreEqual(stackedDepthFirst, composedDepthFirst, "depth-first");

      var composedBreadthFirst = StreamBreadthFirst("a(b(d,e,f),c)")
        .Where(n => n != "b")
        .Where(n => n != "e")
        .GetLevelOrderTraversal().ToArray();

      var stackedBreadthFirst = Tree.DeferBreadthFirst(() => StreamBreadthFirst("a(b(d,e,f),c)").Where(n => n != "b"))
        .Where(n => n != "e")
        .GetLevelOrderTraversal().ToArray();

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
        depthFirst.GetPreorderTraversal().ToArray());
    }

    [TestMethod]
    public void NarrowSelectThenPruneDescendantsWhere_StaysOnTheLightTier()
    {
      IDepthFirstTreenumerable<string> composed = StreamDepthFirst("a(b(d,e),c)")
        .Select(n => n + "!")
        .PruneDescendantsWhere(n => n == "b!");

      Assert.IsInstanceOfType(composed, typeof(SelectPruneDescendantsWhereDepthFirstTreenumerable<string, string>));

      var stacked = Tree.DeferDepthFirst(() => StreamDepthFirst("a(b(d,e),c)").Select(n => n + "!"))
        .PruneDescendantsWhere(n => n == "b!")
        .GetPreorderTraversal().ToArray();

      CollectionAssert.AreEqual(stacked, composed.GetPreorderTraversal().ToArray());
    }

    [TestMethod]
    public void NarrowPruneDescendantsWhereOverPruneDescendantsWhere_StaysOnTheBespokeDriver()
    {
      IDepthFirstTreenumerable<string> depthFirst = StreamDepthFirst("a(b(d),c(e))")
        .PruneDescendantsWhere(n => n == "b")
        .PruneDescendantsWhere(n => n == "c");

      Assert.IsInstanceOfType(depthFirst, typeof(PruneDescendantsWhereDepthFirstTreenumerable<string>));

      IBreadthFirstTreenumerable<string> breadthFirst = StreamBreadthFirst("a(b(d),c(e))")
        .PruneDescendantsWhere(n => n == "b")
        .PruneDescendantsWhere(n => n == "c");

      Assert.IsInstanceOfType(breadthFirst, typeof(PruneDescendantsWhereBreadthFirstTreenumerable<string>));

      CollectionAssert.AreEqual(
        new[] { "a", "b", "c" },
        depthFirst.GetPreorderTraversal().ToArray());
    }

    // The narrow seal pin, flipped with the composite one: rejecting operators splice
    // into ONE narrow driver. (The seal opened 2026-08-18, composite and narrow together --
    // CompositeToNarrow fanned the interface re-merge out; see the composite twin pin.)
    [TestMethod]
    public void NarrowLightTier_JoinsWhenARejectingOperatorArrives_TheSealIsOpen()
    {
      // The narrow seal opened WITH the composite one (2026-08-18): CompositeToNarrow fans
      // the interface re-merge out, so narrow light chains splice into one narrow driver.
      IDepthFirstTreenumerable<string> joined = StreamDepthFirst("a(b(d,e),c)")
        .Select(n => n + "!")
        .PruneDescendantsWhere(n => n == "b!")
        .Where(n => n != "c!");

      Assert.AreEqual(
        typeof(SelectWhereDepthFirstTreenumerable<,,>),
        joined.GetType().GetGenericTypeDefinition(),
        "a rejecting operator joining a narrow light chain must splice into ONE narrow driver");
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
        .GetPreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { "a@0", "c@1" }, labeled);
    }

    // PruneDescendantsWhere is label-preserving, so the narrow positional Select composes across it and
    // the chain stays on the light tier.
    [TestMethod]
    public void NarrowPositionalSelect_ComposesAcrossPruneDescendantsWhere()
    {
      IDepthFirstTreenumerable<string> composed = StreamDepthFirst("a(b(c),d)")
        .PruneDescendantsWhere(n => n == "b")
        .Select((n, position) => $"{n}@{position.Depth}.{position.SiblingIndex}");

      Assert.IsInstanceOfType(composed, typeof(SelectPruneDescendantsWhereDepthFirstTreenumerable<string, string>));

      CollectionAssert.AreEqual(
        new[] { "a@0.0", "b@1.0", "d@1.1" },
        composed.GetPreorderTraversal().ToArray());
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

      Assert.AreEqual(typeof(SelectWhereTreenumerable<,,>), continued.GetType().GetGenericTypeDefinition(),
        "the composite wrapper must keep composing, not stack a narrow layer");

      CollectionAssert.AreEqual(
        new[] { "a", "d", "c" },
        continued.GetPreorderTraversal().ToArray());
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
          .PruneSubtreesWhere(n => n == "D")
          .PruneDescendantsWhere(n => n == "C");

        ITreenumerable<string> engine = TreeSerializer.DeserializeDepthFirstTree(tree)
          .Select(n => n.ToUpperInvariant())
          .Where(n => n != "B")
          .PruneSubtreesWhere(n => n == "D")
          .PruneDescendantsWhere(n => n == "C");

        VisitStreamConformance.AssertSameStream(
          engine.GetDepthFirstTreenumerator(),
          narrowDepthFirst.GetDepthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"narrow composed DFT chain {tree}");

        IBreadthFirstTreenumerable<string> narrowBreadthFirst = StreamBreadthFirst(tree)
          .Select(n => n.ToUpperInvariant())
          .Where(n => n != "B")
          .PruneSubtreesWhere(n => n == "D")
          .PruneDescendantsWhere(n => n == "C");

        VisitStreamConformance.AssertSameStream(
          engine.GetBreadthFirstTreenumerator(),
          narrowBreadthFirst.GetBreadthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"narrow composed BFT chain {tree}");
      }
    }
  }
}
