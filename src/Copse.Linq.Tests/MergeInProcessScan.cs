using Copse.Core;
using Copse.Linq.Treenumerators;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Independent in-process oracle for the Union / StructuralMerge engine, mirroring the structure of
  // Where2InProcessScan: loops a curated + cross-product case set in ONE test (no per-case MSTest
  // discovery), compares a keyed visit-stream tuple sequence, and asserts 0 failures while collecting
  // failure display names.
  //
  // The whole value of this oracle is INDEPENDENCE from the merge engine. For each (left, right):
  //   1. Materialize both operands via the TRUSTED base engine only (never the merge engine).
  //   2. Independently overlay the two forests by sibling index into a PreorderTree<MergeNode<...>>
  //      that represents the expected union STRUCTURE.
  //   3. Expected stream  = overlay.GetTraversal(strategy, selector).
  //   4. Actual stream    = left.Union(right).GetTraversal(strategy, selector).
  //   5. Compare keyed tuple sequences with SequenceEqual (actual bounded by Take to catch hangs).
  //
  // The merge engine is known-buggy; the SUT failing here is EXPECTED. This establishes a baseline.
  [TestClass]
  public class MergeInProcessScan
  {
    public TestContext TestContext { get; set; }

    // Curated (left, right) pairs covering the assessment's named DFT scenarios. Left uses LETTERS,
    // right uses DIGITS (disjoint alphabets) so every merged node has a unique key.
    private static readonly (string Left, string Right)[] CuratedPairs =
    {
      ("a(b(c))",          "0"),
      ("a(b,c)",           "0(1,2)"),
      ("a,b,c",            "0"),
      ("a(b)",             "0(1)"),
      ("a(b,c),d",         "0,1(2,3)"),
      ("a(b(d(e)),c)",     "0(1(2(3)))"),
      ("a",                ""),
      ("",                 "0"),
      ("",                 ""),
      ("a(b,c,d)",         "0(1)"),
      ("a",                "0(1,2)"),
    };

    // Cross-product: left shapes (letters) x right shapes (digits), to widen coverage beyond the curated
    // pairs. The shapes are deliberately mismatched so overlay/promotion behavior is exercised. The
    // "a(b(c,d),e)" / "0(1(2),3)" pair (and its left-fewer-than-right mirror) produce an overlay whose
    // SkipNode'd internal node has left/right copies with UNEQUAL, both-nonzero child counts -- the
    // "divergent-shape lockstep" case where the two operands promote different numbers of children and
    // the following real siblings must be RE-PAIRED after the operands desynchronize.
    private static readonly string[] CrossLeftShapes =
    {
      "a",
      "a(b)",
      "a(b,c)",
      "a(b(c))",
      "a,b(c)",
      "a(b(c,d),e)",
      "a(b(p),e)",
    };

    private static readonly string[] CrossRightShapes =
    {
      "0",
      "0(1,2)",
      "0,1,2",
      "0(1(2))",
      "0,1(2)",
      "0(1(2),3)",
      "0(1(4,5),3)",
    };

    // Broad gate: full AllTreeStrings^2 cross-product, up to 2 simultaneous strategies (~597k cases).
    [TestMethod]
    public void DepthFirstMatchesOracle() => RunScan(TreeTraversalStrategy.DepthFirst, BuildFullPairs(), maxArity: 2);

    [TestMethod]
    public void BreadthFirstMatchesOracle() => RunScan(TreeTraversalStrategy.BreadthFirst, BuildFullPairs(), maxArity: 2);

    // Deep gate: a curated set of multi-level-promotion-prone shapes at up to 3 simultaneous strategies.
    // Arity 3 over the FULL AllTreeStrings^2 set would be ~115M cases; this curated subset reaches the
    // arity-3 behavior (consecutive SkipNode'd ancestors promoting across >1 level, plus a skip on a
    // promoted node) that the arity-2 broad gate structurally cannot construct.
    [TestMethod]
    public void DepthFirstMatchesOracle_Arity3Deep() => RunScan(TreeTraversalStrategy.DepthFirst, BuildDeepPairs(), maxArity: 3);

    [TestMethod]
    public void BreadthFirstMatchesOracle_Arity3Deep() => RunScan(TreeTraversalStrategy.BreadthFirst, BuildDeepPairs(), maxArity: 3);

    private static List<(string Left, string Right)> BuildFullPairs()
    {
      var pairs = new List<(string Left, string Right)>();
      pairs.AddRange(CuratedPairs);
      foreach (var left in CrossLeftShapes)
        foreach (var right in CrossRightShapes)
          pairs.Add((left, right));

      // Full structural cross-product over the exhaustive Where tree set (groups c..i, up to 9 nodes):
      // every left shape (letters) x every right shape (digit-mapped for a disjoint alphabet). This is
      // the broad coverage that turns "no curated gap" into "no structural gap" -- a single SkipNode on
      // any internal merged node, over every pair of mismatched shapes, is exercised here.
      foreach (var left in Where2Tests.AllTreeStrings)
        foreach (var right in Where2Tests.AllTreeStrings)
          pairs.Add((left, DigitMap(right)));

      return pairs;
    }

    // Curated deep shapes whose overlays contain spines of 2+ stackable SkipNode'd ancestors, so an
    // arity-3 strategy assignment can exercise multi-level promotion + a skip on a promoted node.
    private static List<(string Left, string Right)> BuildDeepPairs()
    {
      var deepLeft = new[]
      {
        "a(b(c,d),e)",
        "a(b(c(d)))",
        "a(b(c,d,e))",
        "a(b(d(e)),c)",
        "a(b(c(d(e))))",
      };
      var deepRight = new[]
      {
        "a(b(c(d)))",
        "a(b(c,d),e)",
        "a(b(d,e),c)",
        "a(b(c(d)),e)",
        "a(b(d(e)),c)",
      };

      var pairs = new List<(string Left, string Right)>
      {
        // The reviewer's explicit arity-3 repros (left letters, right digits).
        ("a(b(c,d),e)",       "0(1(2(3)))"),
        ("a(b(d(e)),c)",      "0(1(3,4),2(5(6)))"),
        ("a(b(d,e),c(f(g)))", "0(1(3(4)),2)"),
      };

      foreach (var left in deepLeft)
        foreach (var right in deepRight)
          pairs.Add((left, DigitMap(right)));

      return pairs;
    }

    private void RunScan(TreeTraversalStrategy treeTraversalStrategy, List<(string Left, string Right)> pairs, int maxArity)
    {
      long total = 0;
      long failed = 0;
      var failures = new List<string>();

      // Self-check counters for oracle correctness: how many empty-strategy (TraverseAll-only) cases
      // pass / fail. A correct DFT oracle should pass the simple no-strategy curated cases.
      long traverseAllTotal = 0;
      long traverseAllPassed = 0;
      var traverseAllPassedNames = new List<string>();

      var allStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(s => s != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var (leftString, rightString) in pairs)
      {
        // Materialize both operands using ONLY the trusted base engine.
        var left = TreeSerializer.Deserialize(leftString).Materialize();
        var right = TreeSerializer.Deserialize(rightString).Materialize();

        // Independently reconstruct each operand's forest and overlay them by sibling index.
        var leftForest = BuildForest(left);
        var rightForest = BuildForest(right);
        var overlayForest = Overlay(leftForest, rightForest);

        // Build the expected union structure as a concrete PreorderTree (base engine only).
        var overlay = FlattenToPreorderTree(overlayForest);

        // The merged-node keys (one per node) used for strategy assignment.
        var mergedKeys = CollectKeys(overlayForest);

        // Strategy assignments = all 0-, 1-, and 2-combinations of (mergedNodeKey, strategy).
        var keyStrategyPairs =
          mergedKeys
          .SelectMany(key => allStrategies.Select(strategy => new Where2Tests.NodeAndTraversalStrategy(key, strategy)))
          .ToArray();

        var assignmentCombos =
          Combinatorics.GetCombinationsUpToCount<Where2Tests.NodeAndTraversalStrategy>(keyStrategyPairs.AsSpan(), maxArity);

        foreach (var combo in assignmentCombos)
        {
          var assignment = combo.ToArray();

          // Reject combos that assign more than one strategy to the SAME merged node (ambiguous).
          if (HasDuplicateNode(assignment))
            continue;

          total++;

          NodeTraversalStrategies Selector(NodeContext<MergeNode<string, string>> nodeContext)
          {
            var key = Key(nodeContext.Node);
            foreach (var pair in assignment)
              if (pair.Node == key)
                return pair.NodeTraversalStrategy;
            return NodeTraversalStrategies.TraverseAll;
          }

          var expected = Key(overlay.GetTraversal(treeTraversalStrategy, Selector));
          // Take() bounds a hypothetical non-terminating merge regression into a length mismatch.
          var actual = Key(left.Union(right).GetTraversal(treeTraversalStrategy, Selector)).Take(100_000);

          var matched = expected.SequenceEqual(actual);

          if (assignment.Length == 0)
          {
            traverseAllTotal++;
            if (matched)
            {
              traverseAllPassed++;
              if (traverseAllPassedNames.Count < 40)
                traverseAllPassedNames.Add(DisplayName(leftString, rightString, assignment));
            }
          }

          if (!matched)
          {
            failed++;
            if (failures.Count < 60)
              failures.Add(DisplayName(leftString, rightString, assignment));
          }
        }
      }

      TestContext.WriteLine(
        $"MergeInProcessScan ({treeTraversalStrategy}): {total} cases across {pairs.Count} (left,right) pairs.");
      TestContext.WriteLine(
        $"  Failed: {failed} of {total}.");
      TestContext.WriteLine(
        $"  Oracle self-check (TraverseAll-only cases): {traverseAllPassed} of {traverseAllTotal} matched the SUT.");
      TestContext.WriteLine(
        $"  TraverseAll matches: {string.Join("; ", traverseAllPassedNames)}");
      TestContext.WriteLine("  ----- Failure display names -----");
      foreach (var failure in failures)
        TestContext.WriteLine($"    {failure}");

      Assert.AreEqual(
        0L,
        failed,
        $"{treeTraversalStrategy} Union (StructuralMerge) diverged from the oracle on {failed} of {total} cases "
        + $"(TraverseAll self-check: {traverseAllPassed}/{traverseAllTotal} matched):{Environment.NewLine}"
        + string.Join(Environment.NewLine, failures));
    }

    // ---- Keyed comparison tuple (independent of MergeNode value-equality, which the lib forbids) ----

    private static IEnumerable<(TreenumeratorMode, int, int, int, bool, bool, string, string)> Key(
      IEnumerable<NodeVisit<MergeNode<string, string>>> visits) =>
      visits.Select(visit => (
        visit.Mode,
        visit.Position.Depth,
        visit.Position.SiblingIndex,
        visit.VisitCount,
        visit.Node.HasLeft,
        visit.Node.HasRight,
        visit.Node.HasLeft ? visit.Node.Left : null,
        visit.Node.HasRight ? visit.Node.Right : null));

    // Unique per-node string id. Disjoint alphabets (letters left, digits right) make this unique.
    private static string Key(MergeNode<string, string> m) =>
      (m.HasLeft ? m.Left : "") + (m.HasRight ? m.Right : "");

    // Map a letter-alphabet tree string (a..z) to digits (a->0, b->1, ...) so a right operand reuses the
    // AllTreeStrings shapes while keeping the merge keys disjoint from the letter-valued left operand.
    private static string DigitMap(string treeString)
    {
      var chars = treeString.ToCharArray();
      for (int i = 0; i < chars.Length; i++)
        if (chars[i] >= 'a' && chars[i] <= 'z')
          chars[i] = (char)('0' + (chars[i] - 'a'));
      return new string(chars);
    }

    // True if any two entries target the same merged node (an ambiguous assignment we skip).
    private static bool HasDuplicateNode(Where2Tests.NodeAndTraversalStrategy[] assignment)
    {
      for (int i = 0; i < assignment.Length; i++)
        for (int j = i + 1; j < assignment.Length; j++)
          if (assignment[i].Node == assignment[j].Node)
            return true;
      return false;
    }

    private static string DisplayName(string left, string right, Where2Tests.NodeAndTraversalStrategy[] assignment)
    {
      var strat =
        assignment.Length == 0
        ? "[]"
        : "[" + string.Join(", ", assignment.Select(p => $"{p.Node}:{p.NodeTraversalStrategy}")) + "]";

      return $"L={(left == "" ? "<empty>" : left)} R={(right == "" ? "<empty>" : right)} strat={strat}";
    }

    // ---- Independent overlay model ----

    private sealed class ONode
    {
      public string Value;
      public List<ONode> Children = new List<ONode>();
    }

    private sealed class OMergeNode
    {
      public bool HasLeft;
      public string Left;
      public bool HasRight;
      public string Right;
      public List<OMergeNode> Children = new List<OMergeNode>();
    }

    // Reconstruct a forest of ONode from an ITreenumerable<string> using ONLY the base engine,
    // via the depth-stack open-parent reconstruction (the same proven pattern as Materialize.cs).
    private static List<ONode> BuildForest(ITreenumerable<string> source)
    {
      var roots = new List<ONode>();
      // open[d] = the node currently open at depth d (its descendants are still being read).
      var open = new List<ONode>();

      foreach (var visit in source.GetDepthFirstTraversal())
      {
        // First (pre-order/scheduling) visit of each node only -- same selector TreeSerializer uses.
        if (visit.VisitCount != 1)
          continue;

        var depth = visit.Position.Depth;

        // Trim the open-parent stack back to this node's depth.
        while (open.Count > depth)
          open.RemoveAt(open.Count - 1);

        var node = new ONode { Value = visit.Node };

        if (depth == 0)
          roots.Add(node);
        else
          open[depth - 1].Children.Add(node);

        open.Add(node);
      }

      return roots;
    }

    // Overlay two forests by sibling index at every level (roots paired by root index).
    private static List<OMergeNode> Overlay(List<ONode> left, List<ONode> right)
    {
      var result = new List<OMergeNode>();
      var n = Math.Max(left.Count, right.Count);

      for (int i = 0; i < n; i++)
      {
        var hasLeft = i < left.Count;
        var hasRight = i < right.Count;

        var children =
          Overlay(
            hasLeft ? left[i].Children : EmptyForest,
            hasRight ? right[i].Children : EmptyForest);

        result.Add(new OMergeNode
        {
          HasLeft = hasLeft,
          Left = hasLeft ? left[i].Value : null,
          HasRight = hasRight,
          Right = hasRight ? right[i].Value : null,
          Children = children,
        });
      }

      return result;
    }

    private static readonly List<ONode> EmptyForest = new List<ONode>();

    // Flatten an OMergeNode forest into a concrete PreorderTree<MergeNode<string,string>>, using the
    // same open-stack / backfill subtree-size pattern as Materialize.cs.
    private static PreorderTree<MergeNode<string, string>> FlattenToPreorderTree(List<OMergeNode> forest)
    {
      var values = new List<MergeNode<string, string>>();
      var subtreeSizes = new List<int>();

      void Visit(OMergeNode node)
      {
        var index = values.Count;
        // MergeNode ctor order is (left, right, hasLeft, hasRight) -- confirmed in MergeNode.cs.
        values.Add(new MergeNode<string, string>(node.Left, node.Right, node.HasLeft, node.HasRight));
        subtreeSizes.Add(0); // backfilled once this subtree closes

        foreach (var child in node.Children)
          Visit(child);

        subtreeSizes[index] = values.Count - index;
      }

      foreach (var root in forest)
        Visit(root);

      return new PreorderTree<MergeNode<string, string>>(values.ToArray(), subtreeSizes.ToArray());
    }

    // Collect the unique merged-node keys of an OMergeNode forest, in pre-order.
    private static List<string> CollectKeys(List<OMergeNode> forest)
    {
      var keys = new List<string>();
      var seen = new HashSet<string>();

      void Visit(OMergeNode node)
      {
        var key = (node.HasLeft ? node.Left : "") + (node.HasRight ? node.Right : "");
        if (seen.Add(key))
          keys.Add(key);
        foreach (var child in node.Children)
          Visit(child);
      }

      foreach (var root in forest)
        Visit(root);

      return keys;
    }
  }
}
