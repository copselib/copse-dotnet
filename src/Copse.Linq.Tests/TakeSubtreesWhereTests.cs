using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Linq.Tests
{
  // TakeSubtreesWhere (ratified 2026-08-06 -- the subgraph selection cluster's tree restriction):
  // matched subtrees re-rooted as the result forest, OUTERMOST MATCH WINS (the in-subtree
  // flag: inside a match the predicate never fires -- a tree cannot share substructure, so
  // nested matches suppress rather than absorb; the dag analog TakeSubgraphsWhere gets the same
  // semantics emergently). Roots take the matches' source preorder order; descendants keep
  // their sibling indices with depth shifted. Expected forests are pinned as serialized trees
  // and compared over BOTH traversal dimensions, so positions, sibling renumbering, and visit
  // streams are all under test.
  [TestClass]
  public class TakeSubtreesWhereTests
  {
    // (tree, matched values, expected forest)
    [DataTestMethod]
    [DataRow("a(b(d,e),c(f,g))", "b,c", "b(d,e),c(f,g)", DisplayName = "the ruling example: two disjoint matches re-root")]
    [DataRow("a(b(d(x),e),c)", "b,d", "b(d(x),e)", DisplayName = "nested match dissolves: outermost wins, d is not re-rooted")]
    [DataRow("a(b,c)", "a", "a(b,c)", DisplayName = "matching the root is identity")]
    [DataRow("a(b(d,e),c(f,g))", "z", "", DisplayName = "no matches, empty forest")]
    [DataRow("", "a", "", DisplayName = "empty source, empty forest")]
    [DataRow("a(x),b(y),c", "a,c", "a(x),c", DisplayName = "forest source: matched roots keep their subtrees, sibling indices renumber")]
    [DataRow("r(a(m(x,y)),b(n(z)))", "m,n", "m(x,y),n(z)", DisplayName = "matches at different depths both re-root to depth 0")]
    [DataRow("r(p(x),q,s(y))", "p,s", "p(x),s(y)", DisplayName = "unmatched sibling between matches: roots renumber 0,1")]
    [DataRow("a(b(c(d)))", "b,c,d", "b(c(d))", DisplayName = "a chain of nested matches collapses to the outermost")]
    [DataRow("a(b),c(b(x))", "b", "b,b(x)", DisplayName = "the same value matching twice re-roots twice")]
    public void MatchedSubtreesBecomeTheResultForest(string treeString, string matchedValues, string expectedForest)
    {
      var matches = matchedValues.Split(',');
      Func<string, bool> predicate = node => matches.Contains(node);

      var expected = TreeSerializer.DeserializeDepthFirstTree(expectedForest);
      var actual = TreeSerializer.DeserializeDepthFirstTree(treeString).TakeSubtreesWhere(predicate);

      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          expected.GetTraversal(strategy).ToArray(),
          actual.GetTraversal(strategy).ToArray(),
          $"{strategy} mismatch for '{treeString}' matching [{matchedValues}]");
    }

    [TestMethod]
    public void PositionalFlavor_TheRulingExample_DepthOne()
    {
      // The flavor the ruling was stated in: TakeSubtreesWhere((x, p) => p.Depth == 1) over
      // a(b(d,e),c(f,g)) yields b(d,e),c(f,g).
      var expected = TreeSerializer.DeserializeDepthFirstTree("b(d,e),c(f,g)");
      var actual = TreeSerializer
        .DeserializeDepthFirstTree("a(b(d,e),c(f,g))")
        .TakeSubtreesWhere((_, position) => position.Depth == 1);

      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          expected.GetTraversal(strategy).ToArray(),
          actual.GetTraversal(strategy).ToArray(),
          $"{strategy} mismatch");
    }

    [TestMethod]
    public void PositionalFlavor_SeesSourcePositions_NotResultPositions()
    {
      // The predicate speaks the INPUT tree's labels (the positional-Where rule): matching
      // source sibling index 1 at depth 1 selects only c's subtree.
      var actual = TreeSerializer
        .DeserializeDepthFirstTree("a(b(d),c(e))")
        .TakeSubtreesWhere((_, position) => position.Depth == 1 && position.SiblingIndex == 1);

      CollectionAssert.AreEqual(
        TreeSerializer.DeserializeDepthFirstTree("c(e)").GetPreorderTraversal().ToArray(),
        actual.GetPreorderTraversal().ToArray());
    }

    [TestMethod]
    public void DepthFirstNarrowArm_Streams_AndAgreesWithTheBufferArm()
    {
      var source = "a(b(d,e),c(f,g))";
      Func<string, bool> predicate = node => node == "b" || node == "c";

      var streamed = ((IDepthFirstTreenumerable<string>)TreeSerializer.DeserializeDepthFirstTree(source))
        .TakeSubtreesWhere(predicate);
      var buffered = TreeSerializer.DeserializeDepthFirstTree(source).TakeSubtreesWhere(predicate);

      // The full visit streams, not just values -- positions and visit counts must agree.
      CollectionAssert.AreEqual(
        buffered.GetDepthFirstTraversal().ToArray(),
        streamed.GetDepthFirstTraversal().ToArray());
    }

    [TestMethod]
    public void BreadthFirstNarrowArm_TheDisclosureRule_EqualsTheExplicitEscalation()
    {
      var source = "a(b(d,e),c(f,g))";
      Func<string, bool> predicate = node => node == "b" || node == "c";

      var viaDisclosureRule = ((IBreadthFirstTreenumerable<string>)TreeSerializer.DeserializeDepthFirstTree(source))
        .TakeSubtreesWhere(predicate);
      var viaExplicitEscalation = TreeSerializer.DeserializeDepthFirstTree(source)
        .Materialize()
        .TakeSubtreesWhere(predicate);

      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          viaExplicitEscalation.GetTraversal(strategy).ToArray(),
          viaDisclosureRule.GetTraversal(strategy).ToArray(),
          $"{strategy} mismatch");
    }

    [TestMethod]
    public void ResultComposes_LikeTheEquivalentPlainForest()
    {
      // The composition claim: the result buffer behaves exactly like a REAL forest of the
      // matched subtrees under downstream operators -- pinned by running the same chain over
      // both. (TakeTrees rides TakeNodesUntil, whose truncation is stream-shaped and
      // dimension-dependent in BFT; whatever it does over a plain forest, it must do
      // identically here.)
      var overPlainForest = TreeSerializer
        .DeserializeDepthFirstTree("b(d,e),c(f,g)")
        .TakeTrees(1);

      var overTakeSubtreesWhere = TreeSerializer
        .DeserializeDepthFirstTree("a(b(d,e),c(f,g))")
        .TakeSubtreesWhere(node => node == "b" || node == "c")
        .TakeTrees(1);

      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          overPlainForest.GetTraversal(strategy).ToArray(),
          overTakeSubtreesWhere.GetTraversal(strategy).ToArray(),
          $"{strategy} mismatch");
    }

    [TestMethod]
    public void NullPredicate_Throws()
    {
      Assert.ThrowsException<ArgumentNullException>(
        () => TreeSerializer.DeserializeDepthFirstTree("a").TakeSubtreesWhere((Func<string, bool>)null));
    }
  }
}
