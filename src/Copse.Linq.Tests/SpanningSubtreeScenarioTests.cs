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

      // 4. The LCA fold, climbing -- and the climbs RECORD the paths: the kept-set (every
      //    node on a target-to-LCA path) falls out of the same walks that find the LCA.
      var lca = targets.Aggregate((left, right) => LowestCommonAncestor(walkable, left, right));

      Assert.AreEqual("a", walkable.GetValue(lca), "the three targets' LCA");

      var keptHandles = new HashSet<int> { lca };
      foreach (var target in targets)
        foreach (var pathHandle in PathToAncestor(walkable, target, lca))
          keptHandles.Add(pathHandle);

      // 5. Re-root at the LCA: the region floor's shipped lens, handles shared.
      var spanning = walkable.WalkerAt(lca).Subtree();

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

      var lca = targets.Aggregate((left, right) => LowestCommonAncestor(walkable, left, right));

      Assert.AreEqual("b", walkable.GetValue(lca), "the LCA is mid-tree");

      var keptHandles = new HashSet<int> { lca };
      foreach (var target in targets)
        foreach (var pathHandle in PathToAncestor(walkable, target, lca))
          keptHandles.Add(pathHandle);

      var clamped = walkable.WalkerAt(lca).Subtree()
        .Extend((terrain, handle) => new HandleAndValue<int, string>(handle, terrain.GetValue(handle)))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Value);

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("b(d(h),e)")),
        DrainScheduleOrder(clamped),
        "the spanning subtree of a mid-tree cluster");
    }

    // ---------------------------------------------------------------- the hand-rolled axes
    // These two helpers ARE the axis wave's spec, in its target dialect: LowestCommonAncestor
    // and the ancestor path, by climbing, comparing handles only (the provider's-own-terms
    // clause -- values are never compared by anything below the consumer's own predicate).

    private static int LowestCommonAncestor(IWalkableTreenumerable<string, int> walkable, int first, int second)
    {
      var firstPath = new HashSet<int>();

      for (var stance = walkable.WalkerAt(first); ; )
      {
        firstPath.Add(stance.Focus);

        var up = stance.MoveToParent();
        if (!up.HasWalker)
          break;

        stance = up.Walker;
      }

      var candidate = walkable.WalkerAt(second);
      while (!firstPath.Contains(candidate.Focus))
        candidate = candidate.MoveToParent().Walker;

      return candidate.Focus;
    }

    private static IEnumerable<int> PathToAncestor(IWalkableTreenumerable<string, int> walkable, int descendant, int ancestor)
    {
      var stance = walkable.WalkerAt(descendant);

      while (stance.Focus != ancestor)
      {
        yield return stance.Focus;
        stance = stance.MoveToParent().Walker;
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
