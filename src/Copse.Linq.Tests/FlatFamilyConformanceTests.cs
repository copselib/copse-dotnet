using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Conformance for the flat family's treenumerables (PreorderTreenumerable /
  // LevelOrderTreenumerable) and, through them, all four store treenumerators, over COMPLETED
  // array-backed stores -- independent of the memoize machinery that also rides them. Each
  // wrapper's visit streams (native and cross-order dimension, every strategy on every node)
  // must be identical to the engine's over the same shape. The lockstep pattern is
  // ContractTreeConformanceTests'.
  [TestClass]
  public class FlatFamilyConformanceTests
  {
    private static readonly string[] Trees =
    {
      "",
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a,b(c)",
      "a(b,c,d)",
      "a(b(d(e)),c)",
      "a(b(d,e,f),c(g,h,i))",
      "a(d(g)),b(e(h)),c(f(i))",
      "a,b(d),c(e(f))",
    };

    private static readonly NodeTraversalStrategies[] SchedulingStrategies =
    {
      NodeTraversalStrategies.SkipNode,
      NodeTraversalStrategies.SkipDescendants,
      NodeTraversalStrategies.SkipSiblings,
      NodeTraversalStrategies.SkipNodeAndDescendants,
      NodeTraversalStrategies.SkipNodeAndSiblings,
      NodeTraversalStrategies.SkipDescendants | NodeTraversalStrategies.SkipSiblings,
      NodeTraversalStrategies.SkipAll,
    };

    private delegate NodeTraversalStrategies StrategyScript(TreenumeratorMode mode, string node, int visitCount);

    private static readonly StrategyScript TraverseAll = (mode, node, visitCount) => NodeTraversalStrategies.TraverseAll;

    // ---------------------------------------------------------------------------------------
    // Array-backed stores, built by draining the engine's streams for the same shape.
    // ---------------------------------------------------------------------------------------

    private sealed class ArrayPreorderStore : IPreorderStore<string>
    {
      public ArrayPreorderStore(string[] values, int[] subtreeSizes)
      {
        _Values = values;
        _SubtreeSizes = subtreeSizes;
      }

      private readonly string[] _Values;
      private readonly int[] _SubtreeSizes;

      public bool EnsureBuffered(int index) => index < _Values.Length;
      public int EnsureSubtreeClosed(int index) => _SubtreeSizes[index];
      public int GetSubtreeSize(int index) => _SubtreeSizes[index];
      public string GetValue(int index) => _Values[index];
    }

    private sealed class ArrayLevelOrderStore : ILevelOrderStore<string>
    {
      public ArrayLevelOrderStore(string[] values, int[] firstChildIndices, int[] childCounts, int rootCount)
      {
        _Values = values;
        _FirstChildIndices = firstChildIndices;
        _ChildCounts = childCounts;
        _RootCount = rootCount;
      }

      private readonly string[] _Values;
      private readonly int[] _FirstChildIndices;
      private readonly int[] _ChildCounts;
      private readonly int _RootCount;

      public bool EnsureRootAvailable(int k) => k < _RootCount;
      public bool EnsureChildAvailable(int parentIndex, int k) => k < _ChildCounts[parentIndex];
      public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];
      public string GetValue(int index) => _Values[index];
    }

    // Preorder arrays: values in first-visit order; a parent's subtree size backfills when the
    // next visit lands at its depth or shallower (the serializer/memo open-stack construction).
    private static ArrayPreorderStore BuildPreorderStore(string tree)
    {
      var values = new List<string>();
      var subtreeSizes = new List<int>();
      var open = new Stack<int>();

      using (var treenumerator = TreeSerializer.Deserialize(tree).GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode != TreenumeratorMode.VisitingNode || treenumerator.VisitCount != 1)
            continue;

          var depth = treenumerator.Position.Depth;

          while (open.Count > depth)
          {
            var closed = open.Pop();
            subtreeSizes[closed] = values.Count - closed;
          }

          open.Push(values.Count);
          values.Add(treenumerator.Node);
          subtreeSizes.Add(0);
        }
      }

      while (open.Count > 0)
      {
        var closed = open.Pop();
        subtreeSizes[closed] = values.Count - closed;
      }

      return new ArrayPreorderStore(values.ToArray(), subtreeSizes.ToArray());
    }

    // Level-order arrays: values in scheduling order; every scheduled non-root's parent is the
    // node the feed is currently visiting (the memo level-order-buffer construction).
    private static ArrayLevelOrderStore BuildLevelOrderStore(string tree)
    {
      var values = new List<string>();
      var firstChildIndices = new List<int>();
      var childCounts = new List<int>();
      var rootCount = 0;
      var front = -1;

      using (var treenumerator = TreeSerializer.Deserialize(tree).GetBreadthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            var index = values.Count;

            values.Add(treenumerator.Node);
            firstChildIndices.Add(-1);
            childCounts.Add(0);

            if (treenumerator.Position.Depth == 0)
            {
              rootCount++;
            }
            else
            {
              if (childCounts[front] == 0)
                firstChildIndices[front] = index;

              childCounts[front]++;
            }
          }
          else if (treenumerator.VisitCount == 1)
          {
            front++;
          }
        }
      }

      return new ArrayLevelOrderStore(values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }

    private static ITreenumerable<string> PreorderTreenumerable(string tree)
      => new PreorderTreenumerable<string, ArrayPreorderStore>(BuildPreorderStore(tree));

    private static ITreenumerable<string> LevelOrderTreenumerable(string tree)
      => new LevelOrderTreenumerable<string, ArrayLevelOrderStore>(BuildLevelOrderStore(tree));

    // ---------------------------------------------------------------------------------------
    // Conformance: identical visit streams to the engine, both dimensions, every strategy on
    // every node.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Preorder_TraverseAll_MatchesEngine()
    {
      foreach (var tree in Trees)
      {
        AssertSameStream(TreeSerializer.Deserialize(tree), PreorderTreenumerable(tree), depthFirst: true, TraverseAll, $"preorder {tree}");
        AssertSameStream(TreeSerializer.Deserialize(tree), PreorderTreenumerable(tree), depthFirst: false, TraverseAll, $"preorder {tree}");
      }
    }

    [TestMethod]
    public void LevelOrder_TraverseAll_MatchesEngine()
    {
      foreach (var tree in Trees)
      {
        AssertSameStream(TreeSerializer.Deserialize(tree), LevelOrderTreenumerable(tree), depthFirst: true, TraverseAll, $"levelorder {tree}");
        AssertSameStream(TreeSerializer.Deserialize(tree), LevelOrderTreenumerable(tree), depthFirst: false, TraverseAll, $"levelorder {tree}");
      }
    }

    [TestMethod]
    public void Preorder_EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => AssertStrategyMatrixConforms(PreorderTreenumerable, "preorder", depthFirst: true);

    [TestMethod]
    public void Preorder_EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => AssertStrategyMatrixConforms(PreorderTreenumerable, "preorder", depthFirst: false);

    [TestMethod]
    public void LevelOrder_EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => AssertStrategyMatrixConforms(LevelOrderTreenumerable, "levelorder", depthFirst: true);

    [TestMethod]
    public void LevelOrder_EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => AssertStrategyMatrixConforms(LevelOrderTreenumerable, "levelorder", depthFirst: false);

    private static void AssertStrategyMatrixConforms(
      System.Func<string, ITreenumerable<string>> factory,
      string family,
      bool depthFirst)
    {
      foreach (var tree in Trees)
      {
        var targets = tree.Where(char.IsLetter).Select(c => c.ToString()).Distinct().ToArray();

        foreach (var target in targets)
        {
          foreach (var strategy in SchedulingStrategies)
          {
            NodeTraversalStrategies Script(TreenumeratorMode mode, string node, int visitCount)
              => mode == TreenumeratorMode.SchedulingNode && node == target
                ? strategy
                : NodeTraversalStrategies.TraverseAll;

            AssertSameStream(
              TreeSerializer.Deserialize(tree),
              factory(tree),
              depthFirst,
              Script,
              $"{family} {tree} [{strategy} on '{target}']");
          }
        }
      }
    }

    [TestMethod]
    public void PreEnumerationStateIsTheForestRoot()
    {
      foreach (var tree in Trees)
      {
        foreach (var (family, treenumerable) in new[] { ("preorder", PreorderTreenumerable(tree)), ("levelorder", LevelOrderTreenumerable(tree)) })
        {
          using (var depthFirst = treenumerable.GetDepthFirstTreenumerator())
          using (var breadthFirst = treenumerable.GetBreadthFirstTreenumerator())
          {
            Assert.AreEqual(NodePosition.ForestRoot, depthFirst.Position, $"[{family} '{tree}'] DFT pre-enumeration Position");
            Assert.AreEqual(0, depthFirst.VisitCount, $"[{family} '{tree}'] DFT pre-enumeration VisitCount");
            Assert.AreEqual(TreenumeratorMode.SchedulingNode, depthFirst.Mode, $"[{family} '{tree}'] DFT pre-enumeration Mode");

            Assert.AreEqual(NodePosition.ForestRoot, breadthFirst.Position, $"[{family} '{tree}'] BFT pre-enumeration Position");
            Assert.AreEqual(0, breadthFirst.VisitCount, $"[{family} '{tree}'] BFT pre-enumeration VisitCount");
            Assert.AreEqual(TreenumeratorMode.SchedulingNode, breadthFirst.Mode, $"[{family} '{tree}'] BFT pre-enumeration Mode");
          }
        }
      }
    }

    // Lockstep stream comparison; the strategy for each MoveNext is computed from the visit just
    // emitted (asserted equal for both sides first, so both receive the same strategy).
    private static void AssertSameStream(
      ITreenumerable<string> expectedTree,
      ITreenumerable<string> actualTree,
      bool depthFirst,
      StrategyScript script,
      string context)
    {
      using (var expected = depthFirst ? expectedTree.GetDepthFirstTreenumerator() : expectedTree.GetBreadthFirstTreenumerator())
      using (var actual = depthFirst ? actualTree.GetDepthFirstTreenumerator() : actualTree.GetBreadthFirstTreenumerator())
      {
        var strategies = NodeTraversalStrategies.TraverseAll;
        var dimension = depthFirst ? "DFT" : "BFT";
        var step = 0;

        while (true)
        {
          var expectedMoved = expected.MoveNext(strategies);
          var actualMoved = actual.MoveNext(strategies);

          Assert.AreEqual(expectedMoved, actualMoved, $"[{context}] {dimension} step {step}: MoveNext disagreed");

          if (!expectedMoved)
            return;

          Assert.AreEqual(expected.Mode, actual.Mode, $"[{context}] {dimension} step {step}: Mode");
          Assert.AreEqual(expected.Node, actual.Node, $"[{context}] {dimension} step {step}: Node");
          Assert.AreEqual(expected.VisitCount, actual.VisitCount, $"[{context}] {dimension} step {step}: VisitCount");
          Assert.AreEqual(expected.Position, actual.Position, $"[{context}] {dimension} step {step}: Position");

          strategies = script(expected.Mode, expected.Node, expected.VisitCount);
          step++;
        }
      }
    }
  }
}
