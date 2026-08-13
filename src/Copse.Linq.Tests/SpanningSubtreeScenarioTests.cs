using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // UC-32, the capstone, RUNNING: the minimum spanning subtree of k nodes over a filtered
  // pipeline -- one arc, every floor (docs/WALKER_USE_CASES.md §G). The scenario that drove
  // half the walker tier's design now executes end to end on the shipped surface:
  //
  //   1. streaming algebra feeds the walker a derived tree      (untouched, as designed)
  //   2. Materialize -- the capture IS the walkable             (the buffer re-parent)
  //   3. handle acquisition: the rowid scan                     (GetHandlesWithValues)
  //   4. LCA + path recording by climbing                       (HAND -- the axis wave's spec)
  //   5. re-root at the LCA                                     (Subtree(), the shipped lens)
  //   6. the membership clamp                                   (the HANDLE-DECORATED STREAM:
  //      Extend into (handle, value) pairs, prune in handle-space with the streaming
  //      algebra, project back -- the climbs recorded in step 4 ARE the membership memo)
  //
  // Step 6 is the walkthrough's finding: the "unshipped region-floor gap" is expressible
  // today by composition; the future membership lens is performance sugar (adjacency-side
  // pruning without the decorate/project round trip), not missing capability.
  [TestClass]
  public class SpanningSubtreeScenarioTests
  {
    [TestMethod]
    public void TheCapstone_SpanningSubtreeOfThreeTargets_EveryFloorInOneArc()
    {
      // Source: a(b(d(h,i),e),c(f,g(j))). Targets h, i, g. The spanning subtree (nodes on
      // paths between targets) is a(b(d(h,i)),c(g)): e and f are off every path, and j sits
      // BELOW a leaf target -- between-ness excludes it.
      var source = TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i),e),c(f,g(j)))");

      // 1. The streaming algebra upstream, untouched (identity here; the point is the seam).
      var relevant = source.Where(context => true);

      // 2. The escalation is just Materialize -- captures are never address-poor. ORGANIC:
      //    no layout named, because none is needed -- both forms are lazy, and this
      //    scenario's first act is a probe, which settles the undecided capture preorder
      //    (the ancestry-cheap layout) with nobody choosing it. The declared form (see the
      //    second test) is an axis-cost ELECTION, never a requirement.
      var walkable = relevant.Materialize();

      // 3. Handle acquisition: the front door, one line -- FindHandles folds the rowid idiom
      //    in, and the pledge holds (the predicate is consumer code; the library compares
      //    nothing).
      var interesting = new HashSet<string> { "h", "i", "g" };
      var targets = walkable.FindHandles(value => interesting.Contains(value)).ToList();

      Assert.AreEqual(3, targets.Count, "acquisition found the targets");

      // 4. The LCA fold, WALKER-FIRST (the review's rule: one lift at the boundary, the
      //    whole fold lives in the comonad; the result-typed LCA makes the disjoint-trees
      //    miss a fact, so this line cannot mint a poisoned handle -- fix partiality at the
      //    source, keep the doors trusting). Unwrap is safe here: one tree.
      var lca = targets
        .Select(handle => walkable.WalkerAt(handle))
        .Aggregate((left, right) => LowestCommonAncestor(left, right).Walker);

      Assert.AreEqual("a", lca.GetValue(), "the three targets' LCA");

      //    The climbs RECORD the paths: the kept-set (every node on a target-to-LCA path)
      //    falls out of the same walks -- coordinates again, because a SET is storage.
      var keptHandles = new HashSet<int> { lca.Focus };
      foreach (var target in targets)
        foreach (var pathHandle in PathToAncestor(walkable.WalkerAt(target), lca.Focus))
          keptHandles.Add(pathHandle);

      // 5. Re-root at the LCA -- never left the comonad.
      var spanning = lca.Subtree();

      // 6. The membership clamp -- the handle-decorated stream: Extend stamps every node
      //    with its own (handle, value) pair, PruneBefore cuts whole subtrees whose root
      //    is off every path (membership is downward-closed: off-path implies the whole
      //    subtree is off-path), and Select projects back to values.
      var clamped = spanning
        .Extend((terrain, handle) => new HandleAndValue<int, string>(handle, terrain.GetValue(handle)))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Value);

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i)),c(g))")),
        DrainScheduleOrder(clamped),
        "the spanning subtree, exactly");
    }

    [TestMethod]
    public void TheCapstone_TargetsUnderOneBranch_TheSpanningRootIsNotTheTreeRoot()
    {
      // Targets h and e share the branch under b: the spanning subtree is b(d(h),e) --
      // the re-root lands mid-tree, depths compress, and nothing outside b survives.
      // Declared layout here as the deliberate ELECTION: the climbs want cheap ancestry,
      // so this caller names preorder rather than letting the first act pin it.
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i),e),c(f,g(j)))")
        .Materialize(BufferLayout.Preorder);

      var interesting = new HashSet<string> { "h", "e" };
      var targets = walkable.FindHandles(value => interesting.Contains(value)).ToList();

      var lca = targets
        .Select(handle => walkable.WalkerAt(handle))
        .Aggregate((left, right) => LowestCommonAncestor(left, right).Walker);

      Assert.AreEqual("b", lca.GetValue(), "the LCA is mid-tree");

      var keptHandles = new HashSet<int> { lca.Focus };
      foreach (var target in targets)
        foreach (var pathHandle in PathToAncestor(walkable.WalkerAt(target), lca.Focus))
          keptHandles.Add(pathHandle);

      var clamped = lca.Subtree()
        .Extend((terrain, handle) => new HandleAndValue<int, string>(handle, terrain.GetValue(handle)))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Value);

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("b(d(h),e)")),
        DrainScheduleOrder(clamped),
        "the spanning subtree of a mid-tree cluster");
    }

    // The edge cardinalities, pinned (the review's k = 0 finding): the spanning subtree of
    // NO nodes is the empty forest -- the honest arc guards before the fold, because the
    // seedless Aggregate's own miss is a vocabulary-free LINQ exception. One node folds to
    // itself with zero LCA calls, and its spanning subtree is just the node.
    [TestMethod]
    public void TheCapstone_EdgeCardinalities_ZeroTargetsGuards_OneTargetIsItself()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Materialize(BufferLayout.Preorder);

      // k = 0: acquisition misses; the guard fires where the semantics live, not inside LINQ.
      var none = walkable.FindHandles(value => value == "zzz").ToList();
      Assert.AreEqual(0, none.Count, "no targets -- the arc must not reach the fold");

      // k = 1: the fold is a no-op and the clamp keeps exactly the node.
      var single = walkable.FindHandles(value => value == "d")
        .Select(handle => walkable.WalkerAt(handle))
        .Aggregate((left, right) => LowestCommonAncestor(left, right).Walker);

      Assert.AreEqual("d", single.GetValue(), "one target folds to itself, zero LCA calls");

      var keptHandles = new HashSet<int> { single.Focus };

      var clamped = single.Subtree()
        .Extend((terrain, handle) => new HandleAndValue<int, string>(handle, terrain.GetValue(handle)))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Value);

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("d")),
        DrainScheduleOrder(clamped),
        "the spanning subtree of one node is the node");
    }

    // The review's forest finding, pinned: LCA is PARTIAL on forests -- two stances in
    // disjoint trees have no common ancestor, and the miss is a fact in the result type
    // (the old handle-space helper minted the default walker here and marched on).
    [TestMethod]
    public void TheLcaMiss_DisjointTreesInAForest_IsAFact()
    {
      var forest = TreeSerializer.DeserializeDepthFirstTree("a(b),c(d)").Materialize(BufferLayout.Preorder);

      var b = forest.FindHandle(value => value == "b");
      var d = forest.FindHandle(value => value == "d");
      Assert.IsTrue(b.HasHandle && d.HasHandle);

      var miss = LowestCommonAncestor(forest.WalkerAt(b.Handle), forest.WalkerAt(d.Handle));

      Assert.IsFalse(miss.HasWalker, "disjoint trees: no common ancestor, and the type says so");
    }

    // ---------------------------------------------------------------- the hand-rolled axes
    // These helpers ARE the axis wave's spec, in its target dialect after the review:
    // WALKER-FIRST (stances in, stance out -- the co-Kleisli shape) and RESULT-TYPED where
    // the operation can miss (the disjoint-trees case: an int-returning LCA has no honest
    // miss at all -- throw, -1, or 0-which-is-the-root; the result struct makes the miss a
    // fact and the default-walker poison unrepresentable). Handle comparisons only (the
    // provider's-own-terms clause). One thing the spec helpers CANNOT express that the real
    // in-library extension will: the same-terrain check -- walkers hold their terrain
    // privately, so only code in the walker's own assembly can ReferenceEquals two terrains.

    private static TreeWalkerResult<string, int> LowestCommonAncestor(TreeWalker<string, int> first, TreeWalker<string, int> second)
    {
      var firstPath = new HashSet<int>();
      var stance = first;

      while (true)
      {
        firstPath.Add(stance.Focus);

        var up = stance.MoveToParent();
        if (!up.HasWalker)
          break;

        stance = up.Walker;
      }

      var candidate = second;

      while (!firstPath.Contains(candidate.Focus))
      {
        var up = candidate.MoveToParent();
        if (!up.HasWalker)
          return default;   // disjoint trees: an honest miss, never a default walker

        candidate = up.Walker;
      }

      return new TreeWalkerResult<string, int>(candidate);
    }

    private static IEnumerable<int> PathToAncestor(TreeWalker<string, int> descendant, int ancestorFocus)
    {
      var stance = descendant;

      while (stance.Focus != ancestorFocus)
      {
        yield return stance.Focus;

        var up = stance.MoveToParent();
        if (!up.HasWalker)
          throw new System.InvalidOperationException(
            "the given focus is not an ancestor of the starting stance -- a poisoned handle would have walked past a root here");

        stance = up.Walker;
      }
    }

    private static List<string> DrainScheduleOrder(ITreenumerable<string> tree)
    {
      var schedule = new List<string>();

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            schedule.Add(treenumerator.Node);
      }

      return schedule;
    }
  }
}
