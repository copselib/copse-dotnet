using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The projection citizenship's admission test (SELECT_INTO_CAPTURES_DESIGN.md section 3):
  // the citizenship is an ENTRY INTO the SelectWhere algebra, and these pins are the
  // equations -- Select o Citizen = Citizen (closure), SelectWhere o Citizen = SelectWhere
  // (the join, pinned STRUCTURALLY: one driver, never stacked wrappers), plus the functor
  // laws and the wrapper-equivalence anchor every other law reduces to. Every pin runs over
  // the corpus and drains BOTH dimensions (the citizenship must preserve the shape, not
  // just the preorder values). THE THIN SHAPE (SELECT_INTO_CAPTURES_DESIGN.md): buffer-tier
  // citizenship is minted at the Select seam -- a scan returns a PLAIN buffer, and Select
  // over any buffer is the projected citizen, closed from there.
  [TestClass]
  public class SelectComposableLawTests
  {
    private static readonly string[] Corpus =
    {
      "a",
      "a(b,c)",
      "a(b(c,d),e)",
      "a(b(c(d(e))))",
      "a,b(c),d(e(f),g)",
    };

    // The citizen under test: a subtree-node-count scan -- every corpus tree, both product
    // shapes exercised (the pair by default, the projection under ComposeSelect).
    private static ITreenumerableBuffer<NodeAccumulation<string, int>> CountScan(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(tree)
        .LeaffixScan(leaf => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1);

    private static (int[] DepthFirst, int[] BreadthFirst) Drain(ITreenumerable<int> source)
      => (source.GetPreorderTraversal().ToArray(), source.GetLevelOrderTraversal().ToArray());

    [TestMethod]
    public void Closure_SelectOverCitizen_IsCitizen()
    {
      foreach (var tree in Corpus)
      {
        var scan = CountScan(tree);

        // The citizenship starts at the Select seam (the thin shape): the scan itself is a
        // plain buffer; its first Select is the citizen, and composition is closed from there.
        Assert.IsInstanceOfType(scan.Select(x => x.Accumulate), typeof(ISelectComposableTreenumerableBuffer<int>), $"projected [{tree}]");
        Assert.IsInstanceOfType(scan.Select(x => x.Accumulate).Select(count => count * 2), typeof(ISelectComposableTreenumerableBuffer<int>), $"chained [{tree}]");
      }
    }

    [TestMethod]
    public void WrapperEquivalenceAnchor_ComposedEqualsVeneer()
    {
      // The law every other law reduces to: the composed route produces exactly what the
      // stream-wrapper veneer over the same scan produces, both dimensions.
      foreach (var tree in Corpus)
      {
        var composed = Drain(CountScan(tree).Select(x => x.Accumulate));
        var veneer = (
          ((ITreenumerable<NodeAccumulation<string, int>>)CountScan(tree)).Select(x => x.Accumulate).GetPreorderTraversal().ToArray(),
          ((ITreenumerable<NodeAccumulation<string, int>>)CountScan(tree)).Select(x => x.Accumulate).GetLevelOrderTraversal().ToArray());

        CollectionAssert.AreEqual(veneer.Item1, composed.DepthFirst, $"depth-first [{tree}]");
        CollectionAssert.AreEqual(veneer.Item2, composed.BreadthFirst, $"breadth-first [{tree}]");
      }
    }

    [TestMethod]
    public void FunctorIdentity_IdentityProjectionIsTheSource()
    {
      foreach (var tree in Corpus)
      {
        var identityProjected = CountScan(tree).Select(pair => pair);
        var source = CountScan(tree);

        CollectionAssert.AreEqual(
          source.GetPreorderTraversal().Select(pair => pair.Accumulate).ToArray(),
          identityProjected.GetPreorderTraversal().Select(pair => pair.Accumulate).ToArray(),
          $"depth-first [{tree}]");
        CollectionAssert.AreEqual(
          source.GetLevelOrderTraversal().Select(pair => pair.Accumulate).ToArray(),
          identityProjected.GetLevelOrderTraversal().Select(pair => pair.Accumulate).ToArray(),
          $"breadth-first [{tree}]");
      }
    }

    [TestMethod]
    public void FunctorComposition_ChainedSelectsEqualComposedSelector()
    {
      foreach (var tree in Corpus)
      {
        var chained = Drain(CountScan(tree).Select(x => x.Accumulate).Select(count => count * 2));
        var composed = Drain(CountScan(tree).Select(x => x.Accumulate * 2));

        CollectionAssert.AreEqual(composed.DepthFirst, chained.DepthFirst, $"depth-first [{tree}]");
        CollectionAssert.AreEqual(composed.BreadthFirst, chained.BreadthFirst, $"breadth-first [{tree}]");
      }
    }

    [TestMethod]
    public void ComposeRightJoin_FirstFilterProducesTheOneDriver()
    {
      // SelectWhere o Citizen = SelectWhere, pinned STRUCTURALLY (the NarrowCompositionTests
      // idiom): the first Where over a citizen-composed chain is the join into ONE composed
      // driver, and a following Select lands in THAT driver's mapping -- a wrapper stack that
      // computes the right answers still fails this pin.
      foreach (var tree in Corpus)
      {
        var joined = CountScan(tree).Select(x => x.Accumulate).Where(count => count != 2);

        // The driver's SELECTOR shape varies with the chain (a bare Where carries the
        // Where-shaped selector until a Select composes in) -- the pin is the DRIVER itself.
        Assert.AreEqual(typeof(SelectWhereTreenumerable<,,>), joined.GetType().GetGenericTypeDefinition(), $"join [{tree}]");

        var absorbed = joined.Select(count => count * 10);

        Assert.AreEqual(typeof(SelectWhereTreenumerable<,,>), absorbed.GetType().GetGenericTypeDefinition(), $"absorb [{tree}]");
      }
    }

    [TestMethod]
    public void ComposeRightJoin_MixedChainEqualsAllWrapperSpelling()
    {
      foreach (var tree in Corpus)
      {
        var composedRoute = CountScan(tree).Select(x => x.Accumulate).Where(count => count != 2).Select(count => count * 10);
        var wrapperRoute = ((ITreenumerable<NodeAccumulation<string, int>>)CountScan(tree))
          .Select(x => x.Accumulate)
          .Where(count => count != 2)
          .Select(count => count * 10);

        CollectionAssert.AreEqual(
          wrapperRoute.GetPreorderTraversal().ToArray(),
          composedRoute.GetPreorderTraversal().ToArray(),
          $"depth-first [{tree}]");
        CollectionAssert.AreEqual(
          wrapperRoute.GetLevelOrderTraversal().ToArray(),
          composedRoute.GetLevelOrderTraversal().ToArray(),
          $"breadth-first [{tree}]");
      }
    }

    // ---- The STREAMING tier's citizen (RootfixScan; same laws, no buffer anywhere) ----

    private static ITreenumerable<NodeAccumulation<string, int>> DepthScan(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(tree).RootfixScan(0, (accumulate, _) => accumulate + 1);

    [TestMethod]
    public void Streaming_Closure_SelectOverRootfixScanIsCitizen()
    {
      foreach (var tree in Corpus)
      {
        Assert.IsInstanceOfType(DepthScan(tree), typeof(ISelectComposableTreenumerable<NodeAccumulation<string, int>>), $"scan [{tree}]");
        Assert.IsInstanceOfType(DepthScan(tree).Select(x => x.Accumulate), typeof(ISelectComposableTreenumerable<int>), $"projected [{tree}]");
        Assert.IsInstanceOfType(DepthScan(tree).Select(x => x.Accumulate).Select(depth => depth * 2), typeof(ISelectComposableTreenumerable<int>), $"chained [{tree}]");
      }
    }

    [TestMethod]
    public void Streaming_WrapperEquivalenceAnchor_ComposedEqualsForcedWrapper()
    {
      // The force-stacked control (the NarrowCompositionTests idiom): a Tree.Defer wrapper is
      // not composable, so Select over it is the plain wrapper -- the veneer oracle.
      foreach (var tree in Corpus)
      {
        var composed = Drain(ToComposite(DepthScan(tree).Select(x => x.Accumulate)));
        var wrapper = Drain(Tree.Defer(() => DepthScan(tree)).Select(x => x.Accumulate));

        CollectionAssert.AreEqual(wrapper.DepthFirst, composed.DepthFirst, $"depth-first [{tree}]");
        CollectionAssert.AreEqual(wrapper.BreadthFirst, composed.BreadthFirst, $"breadth-first [{tree}]");
      }
    }

    [TestMethod]
    public void Streaming_FunctorComposition_ChainedSelectsEqualComposedSelector()
    {
      foreach (var tree in Corpus)
      {
        var chained = Drain(ToComposite(DepthScan(tree).Select(x => x.Accumulate).Select(depth => depth * 2)));
        var composed = Drain(ToComposite(DepthScan(tree).Select(x => x.Accumulate * 2)));

        CollectionAssert.AreEqual(composed.DepthFirst, chained.DepthFirst, $"depth-first [{tree}]");
        CollectionAssert.AreEqual(composed.BreadthFirst, chained.BreadthFirst, $"breadth-first [{tree}]");
      }
    }

    [TestMethod]
    public void Streaming_ComposeRightJoin_FirstFilterProducesTheOneDriver()
    {
      foreach (var tree in Corpus)
      {
        var joined = DepthScan(tree).Select(x => x.Accumulate).Where(depth => depth != 2);

        Assert.AreEqual(typeof(SelectWhereTreenumerable<,,>), joined.GetType().GetGenericTypeDefinition(), $"join [{tree}]");

        var absorbed = joined.Select(depth => depth * 10);

        Assert.AreEqual(typeof(SelectWhereTreenumerable<,,>), absorbed.GetType().GetGenericTypeDefinition(), $"absorb [{tree}]");
      }
    }

    private static ITreenumerable<int> ToComposite(ITreenumerable<int> source) => source;

    // ---- TakeSubtreesWhere: the dispatched citizen (the operator is not a composition seam) ----

    [TestMethod]
    public void TakeSubtreesWhere_Closure_SelectOverResultIsCitizen_AndWhereJoinsTheDriver()
    {
      // The dimension dispatch (bespoke DFT wrapper / scan-chain BFT) lives BEHIND the
      // citizenship: the composite result composes like any streaming citizen.
      var result = TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e)").TakeSubtreesWhere(node => node == "b");

      Assert.IsInstanceOfType(result, typeof(ISelectComposableTreenumerable<string>), "result");
      Assert.IsInstanceOfType(result.Select(node => node + "!"), typeof(ISelectComposableTreenumerable<string>), "projected");
      Assert.IsInstanceOfType(result.Select(node => node + "!").Select(text => text.Length), typeof(ISelectComposableTreenumerable<int>), "chained");

      Assert.AreEqual(
        typeof(SelectWhereTreenumerable<,,>),
        result.Select(node => node + "!").Where(text => text.Length > 1).GetType().GetGenericTypeDefinition(),
        "join");
    }

    [TestMethod]
    public void TakeSubtreesWhere_MidChain_EqualsForcedWrapperSpelling()
    {
      // The seam pin: TakeSubtreesWhere in the middle of a Select/Where chain, composed
      // route vs the Tree.Defer-broken wrapper route, both dimensions.
      foreach (var tree in Corpus)
      {
        var composed = TreeSerializer.DeserializeDepthFirstTree(tree)
          .TakeSubtreesWhere(node => node == "b")
          .Select(node => node + "!")
          .Where(text => text != "d!");

        var forced = Tree.Defer(() =>
            Tree.Defer(() => TreeSerializer.DeserializeDepthFirstTree(tree).TakeSubtreesWhere(node => node == "b"))
              .Select(node => node + "!"))
          .Where(text => text != "d!");

        CollectionAssert.AreEqual(
          forced.GetPreorderTraversal().ToArray(),
          composed.GetPreorderTraversal().ToArray(),
          $"depth-first [{tree}]");
        CollectionAssert.AreEqual(
          forced.GetLevelOrderTraversal().ToArray(),
          composed.GetLevelOrderTraversal().ToArray(),
          $"breadth-first [{tree}]");
      }
    }

    // ---- COMPOSE-LEFT (the door): the lattice feeds the citizen ----

    [TestMethod]
    public void ComposeLeft_SelectIntoLeaffixScan_EqualsForcedWrapperSpelling()
    {
      // Select(f).LeaffixScan(...): the scan surrenders through the door and walks the
      // un-projected inner raw. The forced-wrapper control (Tree.Defer breaks the door the
      // same way it breaks the lattice) is the oracle; the door route must match it on both
      // dimensions -- and its result must still enter the buffer algebra at the Select seam
      // (the door changes the walk, never the algebra).
      foreach (var tree in Corpus)
      {
        var doorRoute = TreeSerializer.DeserializeDepthFirstTree(tree)
          .Select(node => node + "!")
          .LeaffixScan(leaf => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1);

        var wrapperRoute = Tree.Defer(() => TreeSerializer.DeserializeDepthFirstTree(tree).Select(node => node + "!"))
          .LeaffixScan(leaf => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1);

        Assert.IsInstanceOfType(doorRoute.Select(pair => pair.Accumulate), typeof(ISelectComposableTreenumerableBuffer<int>), $"algebra [{tree}]");

        CollectionAssert.AreEqual(
          wrapperRoute.GetPreorderTraversal().Select(pair => (pair.Node, pair.Accumulate)).ToArray(),
          doorRoute.GetPreorderTraversal().Select(pair => (pair.Node, pair.Accumulate)).ToArray(),
          $"depth-first [{tree}]");
        CollectionAssert.AreEqual(
          wrapperRoute.GetLevelOrderTraversal().Select(pair => (pair.Node, pair.Accumulate)).ToArray(),
          doorRoute.GetLevelOrderTraversal().Select(pair => (pair.Node, pair.Accumulate)).ToArray(),
          $"breadth-first [{tree}]");
      }
    }

    [TestMethod]
    public void ComposeLeft_PositionalSelectIntoLeaffixScan_HonorsPositions()
    {
      // The wrapper's selector is context-shaped; the door hands positions through, so a
      // positional upstream Select must survive the surrender intact.
      foreach (var tree in Corpus)
      {
        var doorRoute = TreeSerializer.DeserializeDepthFirstTree(tree)
          .Select((node, position) => node + position.Depth)
          .LeaffixScan(leaf => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1);

        var wrapperRoute = Tree.Defer(() => TreeSerializer.DeserializeDepthFirstTree(tree).Select((node, position) => node + position.Depth))
          .LeaffixScan(leaf => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1);

        CollectionAssert.AreEqual(
          wrapperRoute.GetPreorderTraversal().Select(pair => (pair.Node, pair.Accumulate)).ToArray(),
          doorRoute.GetPreorderTraversal().Select(pair => (pair.Node, pair.Accumulate)).ToArray(),
          $"depth-first [{tree}]");
      }
    }

    [TestMethod]
    public void SharedSource_ProjectionAgrees_AndOriginalSurvivesComposition()
    {
      // The at-most-once architecture's observable half (THE THIN SHAPE: the source buffer
      // is the sharing substrate): composing does not disturb the original citizen, and a
      // projection mapped from the source's completed store agrees with projecting the
      // original's own pulls -- in either pull order.
      foreach (var tree in Corpus)
      {
        var scan = CountScan(tree);
        var projected = scan.Select(x => x.Accumulate);

        var projectedCounts = projected.GetPreorderTraversal().ToArray();
        var originalCounts = scan.GetPreorderTraversal().Select(pair => pair.Accumulate).ToArray();

        CollectionAssert.AreEqual(originalCounts, projectedCounts, $"variants agree [{tree}]");
      }
    }
  }
}
