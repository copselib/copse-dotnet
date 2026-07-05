using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Conformance for the flat family's random-access tier (PreorderTreenumerable /
  // LevelOrderTreenumerable) and, through them, all four store treenumerators, over COMPLETED
  // array-backed stores -- independent of the memoize machinery that also rides them. Thin
  // adapter over VisitStreamConformance (the shared visit-contract battery).
  [TestClass]
  public class FlatFamilyConformanceTests
  {
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

    private static ITreenumerable<string> Preorder(string tree)
      => new PreorderTreenumerable<string, ArrayPreorderStore>(BuildPreorderStore(tree));

    private static ITreenumerable<string> LevelOrder(string tree)
      => new LevelOrderTreenumerable<string, ArrayLevelOrderStore>(BuildLevelOrderStore(tree));

    // ---------------------------------------------------------------------------------------
    // Conformance: both dimensions of both wrappers, TraverseAll and the full strategy matrix.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Preorder_TraverseAll_MatchesEngine()
    {
      VisitStreamConformance.AssertTraverseAllConforms(tree => Preorder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "preorder");
      VisitStreamConformance.AssertTraverseAllConforms(tree => Preorder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "preorder");
    }

    [TestMethod]
    public void LevelOrder_TraverseAll_MatchesEngine()
    {
      VisitStreamConformance.AssertTraverseAllConforms(tree => LevelOrder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "levelorder");
      VisitStreamConformance.AssertTraverseAllConforms(tree => LevelOrder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "levelorder");
    }

    [TestMethod]
    public void Preorder_EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => Preorder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "preorder");

    [TestMethod]
    public void Preorder_EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => Preorder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "preorder");

    [TestMethod]
    public void LevelOrder_EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => LevelOrder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "levelorder");

    [TestMethod]
    public void LevelOrder_EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => LevelOrder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "levelorder");

    [TestMethod]
    public void PreEnumerationStateIsTheForestRoot()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(Preorder(tree).GetDepthFirstTreenumerator(), $"preorder '{tree}' DFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(Preorder(tree).GetBreadthFirstTreenumerator(), $"preorder '{tree}' BFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(LevelOrder(tree).GetDepthFirstTreenumerator(), $"levelorder '{tree}' DFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(LevelOrder(tree).GetBreadthFirstTreenumerator(), $"levelorder '{tree}' BFT");
      }
    }
  }
}
