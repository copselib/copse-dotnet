using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Conformance for the flat family's STREAMING tier (PreorderStreamTreenumerable /
  // LevelOrderStreamTreenumerable) over forward-only fake sources: each type's single native
  // dimension must produce visit streams identical to the engine's, every strategy on every
  // node. Also proves the ownership contract (disposing the treenumerator disposes its stream;
  // each acquisition pulls a fresh stream from the factory).
  [TestClass]
  public class FlatFamilyStreamConformanceTests
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
    // Forward-only fake sources over in-memory arrays.
    // ---------------------------------------------------------------------------------------

    private sealed class ArrayPreorderStream : IPreorderStream<string>
    {
      public ArrayPreorderStream((string Value, int Depth)[] nodes)
      {
        _Nodes = nodes;
      }

      private readonly (string Value, int Depth)[] _Nodes;
      private int _Cursor;

      public bool Disposed { get; private set; }

      public bool TryReadNext(out string value, out int depth)
      {
        if (_Cursor >= _Nodes.Length)
        {
          value = default;
          depth = default;
          return false;
        }

        (value, depth) = _Nodes[_Cursor++];
        return true;
      }

      public bool TrySkipToDepth(int maxDepth, out string value, out int depth)
      {
        while (_Cursor < _Nodes.Length && _Nodes[_Cursor].Depth > maxDepth)
          _Cursor++;

        return TryReadNext(out value, out depth);
      }

      public void Dispose() => Disposed = true;
    }

    private sealed class ArrayLevelOrderStream : ILevelOrderStream<string>
    {
      public ArrayLevelOrderStream(string[][] groups)
      {
        _Groups = groups;
      }

      private readonly string[][] _Groups;
      private int _Group;
      private int _Item;

      public bool Disposed { get; private set; }

      public bool TryReadNextInGroup(out string value)
      {
        if (_Group >= _Groups.Length || _Item >= _Groups[_Group].Length)
        {
          value = default;
          return false;
        }

        value = _Groups[_Group][_Item++];
        return true;
      }

      public int SkipGroupRemainder()
      {
        if (_Group >= _Groups.Length)
          return 0;

        var remaining = _Groups[_Group].Length - _Item;
        _Item = _Groups[_Group].Length;
        return remaining;
      }

      public bool TryMoveToNextGroup()
      {
        if (_Group + 1 >= _Groups.Length)
          return false;

        _Group++;
        _Item = 0;
        return true;
      }

      public void Dispose() => Disposed = true;
    }

    // Preorder (value, depth) pairs via the engine's DFT stream.
    private static (string, int)[] BuildPreorderNodes(string tree)
    {
      var nodes = new List<(string, int)>();

      using (var treenumerator = TreeSerializer.Deserialize(tree).GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.VisitingNode && treenumerator.VisitCount == 1)
            nodes.Add((treenumerator.Node, treenumerator.Position.Depth));

      return nodes.ToArray();
    }

    // Level-order child groups (group 0 = roots, group k+1 = children of node k) via the
    // engine's BFT stream, trailing empty groups elided to exercise that encoding freedom.
    private static string[][] BuildLevelOrderGroups(string tree)
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

      var groups = new List<string[]> { values.Take(rootCount).ToArray() };

      for (int i = 0; i < values.Count; i++)
        groups.Add(childCounts[i] == 0
          ? System.Array.Empty<string>()
          : values.Skip(firstChildIndices[i]).Take(childCounts[i]).ToArray());

      while (groups.Count > 0 && groups[groups.Count - 1].Length == 0)
        groups.RemoveAt(groups.Count - 1);

      return groups.ToArray();
    }

    private static IDepthFirstTreenumerable<string> PreorderStream(string tree)
      => new PreorderStreamTreenumerable<string, ArrayPreorderStream>(
        () => new ArrayPreorderStream(BuildPreorderNodes(tree)));

    private static IBreadthFirstTreenumerable<string> LevelOrderStream(string tree)
      => new LevelOrderStreamTreenumerable<string, ArrayLevelOrderStream>(
        () => new ArrayLevelOrderStream(BuildLevelOrderGroups(tree)));

    // ---------------------------------------------------------------------------------------
    // Conformance in each type's single native dimension.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PreorderStream_TraverseAll_MatchesEngine()
    {
      foreach (var tree in Trees)
        AssertSameStream(
          TreeSerializer.Deserialize(tree).GetDepthFirstTreenumerator(),
          PreorderStream(tree).GetDepthFirstTreenumerator(),
          TraverseAll,
          $"preorder-stream {tree}");
    }

    [TestMethod]
    public void LevelOrderStream_TraverseAll_MatchesEngine()
    {
      foreach (var tree in Trees)
        AssertSameStream(
          TreeSerializer.Deserialize(tree).GetBreadthFirstTreenumerator(),
          LevelOrderStream(tree).GetBreadthFirstTreenumerator(),
          TraverseAll,
          $"levelorder-stream {tree}");
    }

    [TestMethod]
    public void PreorderStream_EveryNodeEveryStrategy_MatchesEngine()
    {
      ForEachStrategyCase((tree, script, context) =>
        AssertSameStream(
          TreeSerializer.Deserialize(tree).GetDepthFirstTreenumerator(),
          PreorderStream(tree).GetDepthFirstTreenumerator(),
          script,
          $"preorder-stream {context}"));
    }

    [TestMethod]
    public void LevelOrderStream_EveryNodeEveryStrategy_MatchesEngine()
    {
      ForEachStrategyCase((tree, script, context) =>
        AssertSameStream(
          TreeSerializer.Deserialize(tree).GetBreadthFirstTreenumerator(),
          LevelOrderStream(tree).GetBreadthFirstTreenumerator(),
          script,
          $"levelorder-stream {context}"));
    }

    private static void ForEachStrategyCase(Action<string, StrategyScript, string> assert)
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

            assert(tree, Script, $"{tree} [{strategy} on '{target}']");
          }
        }
      }
    }

    // ---------------------------------------------------------------------------------------
    // Ownership and acquisition contracts.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void DisposingTheTreenumeratorDisposesItsStream()
    {
      var preorderStream = new ArrayPreorderStream(BuildPreorderNodes("a(b,c)"));
      using (new Copse.Treenumerators.PreorderStreamDepthFirstTreenumerator<string, ArrayPreorderStream>(preorderStream)) { }
      Assert.IsTrue(preorderStream.Disposed, "preorder stream not disposed");

      var levelOrderStream = new ArrayLevelOrderStream(BuildLevelOrderGroups("a(b,c)"));
      using (new Copse.Treenumerators.LevelOrderStreamBreadthFirstTreenumerator<string, ArrayLevelOrderStream>(levelOrderStream)) { }
      Assert.IsTrue(levelOrderStream.Disposed, "level-order stream not disposed");
    }

    [TestMethod]
    public void EachAcquisitionPullsAFreshStreamFromTheFactory()
    {
      var factoryCalls = 0;

      var treenumerable = new PreorderStreamTreenumerable<string, ArrayPreorderStream>(
        () => { factoryCalls++; return new ArrayPreorderStream(BuildPreorderNodes("a(b,c)")); });

      using (treenumerable.GetDepthFirstTreenumerator()) { }
      using (treenumerable.GetDepthFirstTreenumerator()) { }

      Assert.AreEqual(2, factoryCalls);
    }

    [TestMethod]
    public void PreEnumerationStateIsTheForestRoot()
    {
      foreach (var tree in Trees)
      {
        using (var depthFirst = PreorderStream(tree).GetDepthFirstTreenumerator())
        {
          Assert.AreEqual(NodePosition.ForestRoot, depthFirst.Position, $"['{tree}'] preorder-stream pre-enumeration Position");
          Assert.AreEqual(0, depthFirst.VisitCount, $"['{tree}'] preorder-stream pre-enumeration VisitCount");
          Assert.AreEqual(TreenumeratorMode.SchedulingNode, depthFirst.Mode, $"['{tree}'] preorder-stream pre-enumeration Mode");
        }

        using (var breadthFirst = LevelOrderStream(tree).GetBreadthFirstTreenumerator())
        {
          Assert.AreEqual(NodePosition.ForestRoot, breadthFirst.Position, $"['{tree}'] levelorder-stream pre-enumeration Position");
          Assert.AreEqual(0, breadthFirst.VisitCount, $"['{tree}'] levelorder-stream pre-enumeration VisitCount");
          Assert.AreEqual(TreenumeratorMode.SchedulingNode, breadthFirst.Mode, $"['{tree}'] levelorder-stream pre-enumeration Mode");
        }
      }
    }

    // Lockstep stream comparison; the strategy for each MoveNext is computed from the visit just
    // emitted (asserted equal for both sides first, so both receive the same strategy).
    private static void AssertSameStream(
      ITreenumerator<string> expected,
      ITreenumerator<string> actual,
      StrategyScript script,
      string context)
    {
      using (expected)
      using (actual)
      {
        var strategies = NodeTraversalStrategies.TraverseAll;
        var step = 0;

        while (true)
        {
          var expectedMoved = expected.MoveNext(strategies);
          var actualMoved = actual.MoveNext(strategies);

          Assert.AreEqual(expectedMoved, actualMoved, $"[{context}] step {step}: MoveNext disagreed");

          if (!expectedMoved)
            return;

          Assert.AreEqual(expected.Mode, actual.Mode, $"[{context}] step {step}: Mode");
          Assert.AreEqual(expected.Node, actual.Node, $"[{context}] step {step}: Node");
          Assert.AreEqual(expected.VisitCount, actual.VisitCount, $"[{context}] step {step}: VisitCount");
          Assert.AreEqual(expected.Position, actual.Position, $"[{context}] step {step}: Position");

          strategies = script(expected.Mode, expected.Node, expected.VisitCount);
          step++;
        }
      }
    }
  }
}
