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
  // dimension must produce visit streams identical to the engine's. Thin adapter over
  // VisitStreamConformance, plus the ownership contracts (disposing the treenumerator disposes
  // its stream; each acquisition pulls a fresh stream from the factory).
  [TestClass]
  public class FlatFamilyStreamConformanceTests
  {
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

      public PreorderRead<string> TryReadNext()
      {
        if (_Cursor >= _Nodes.Length)
          return default;

        var (value, depth) = _Nodes[_Cursor++];
        return new PreorderRead<string>(value, depth);
      }

      public PreorderRead<string> TrySkipToDepth(int maxDepth)
      {
        while (_Cursor < _Nodes.Length && _Nodes[_Cursor].Depth > maxDepth)
          _Cursor++;

        return TryReadNext();
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

      public LevelOrderRead<string> TryReadNextInGroup()
      {
        if (_Group >= _Groups.Length || _Item >= _Groups[_Group].Length)
          return default;

        return new LevelOrderRead<string>(_Groups[_Group][_Item++]);
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

      using (var treenumerator = TreeSerializer.DeserializeDepthFirstTree(tree).GetDepthFirstTreenumerator())
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

      using (var treenumerator = TreeSerializer.DeserializeDepthFirstTree(tree).GetBreadthFirstTreenumerator())
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
          ? Array.Empty<string>()
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
      => VisitStreamConformance.AssertTraverseAllConforms(tree => PreorderStream(tree).GetDepthFirstTreenumerator(), depthFirst: true, "preorder-stream");

    [TestMethod]
    public void LevelOrderStream_TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(tree => LevelOrderStream(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "levelorder-stream");

    [TestMethod]
    public void PreorderStream_EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => PreorderStream(tree).GetDepthFirstTreenumerator(), depthFirst: true, "preorder-stream");

    [TestMethod]
    public void LevelOrderStream_EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => LevelOrderStream(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "levelorder-stream");

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
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(PreorderStream(tree).GetDepthFirstTreenumerator(), $"preorder-stream '{tree}'");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(LevelOrderStream(tree).GetBreadthFirstTreenumerator(), $"levelorder-stream '{tree}'");
      }
    }
  }
}
