using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The engine-free contract tests. ContractTree implements ITreenumerable directly (no engine,
  // no child enumerators); these tests prove (a) it conforms to the engine's visit-stream
  // contract under every traversal strategy, and (b) the operator suite behaves identically
  // over it -- i.e. operators depend only on the abstract contract, so ANY conforming
  // implementation works. (b) is the enforcement test for the package-architecture principle
  // that Linq's independence from the engine is semantic (see PACKAGE_ARCHITECTURE.md).
  [TestClass]
  public class ContractTreeConformanceTests
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

    private const string RichTree = "a(b(d,e,f),c(g,h,i))";
    private const string RichForest = "a(d(g)),b(e(h)),c(f(i))";

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

    // The strategy to pass to the NEXT MoveNext, chosen from the visit just emitted.
    private delegate NodeTraversalStrategies StrategyScript(TreenumeratorMode mode, string node, int visitCount);

    private static readonly StrategyScript TraverseAll = (mode, node, visitCount) => NodeTraversalStrategies.TraverseAll;

    // ---------------------------------------------------------------------------------------
    // Direct conformance: ContractTree's visit streams are identical to the engine's
    // (TreeSerializer.Deserialize -> PreorderTree -> DFS/BFS engine) for the same shape.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void TraverseAll_MatchesEngine()
    {
      foreach (var tree in Trees)
      {
        AssertSameStream(TreeSerializer.Deserialize(tree), ContractTree.Parse(tree), depthFirst: true, TraverseAll, tree);
        AssertSameStream(TreeSerializer.Deserialize(tree), ContractTree.Parse(tree), depthFirst: false, TraverseAll, tree);
      }
    }

    [TestMethod]
    public void EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => AssertStrategyMatrixConforms(depthFirst: true);

    [TestMethod]
    public void EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => AssertStrategyMatrixConforms(depthFirst: false);

    private static void AssertStrategyMatrixConforms(bool depthFirst)
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
              ContractTree.Parse(tree),
              depthFirst,
              Script,
              $"{tree} [{strategy} on '{target}']");
          }
        }
      }
    }

    [TestMethod]
    public void StrategiesOnVisitingVisitsAreIgnored()
    {
      // Both implementations must ignore flags passed while positioned on a visiting visit;
      // feed deterministic junk on every visiting visit and require identical streams.
      NodeTraversalStrategies Script(TreenumeratorMode mode, string node, int visitCount)
        => mode == TreenumeratorMode.VisitingNode
          ? SchedulingStrategies[(node[0] + visitCount) % SchedulingStrategies.Length]
          : NodeTraversalStrategies.TraverseAll;

      foreach (var tree in Trees.Where(t => t.Length > 0))
      {
        AssertSameStream(TreeSerializer.Deserialize(tree), ContractTree.Parse(tree), depthFirst: true, Script, tree);
        AssertSameStream(TreeSerializer.Deserialize(tree), ContractTree.Parse(tree), depthFirst: false, Script, tree);
      }
    }

    // ---------------------------------------------------------------------------------------
    // Operator conformance: every operator produces identical results over the engine-backed
    // tree and the engine-free tree. This is the semantic-independence enforcement test.
    // ---------------------------------------------------------------------------------------

    private static readonly (string Name, Func<ITreenumerable<string>, ITreenumerable<string>> Op)[] TreeOperators =
    {
      ("Where(!= b)", t => t.Where(nodeContext => nodeContext.Node != "b")),
      ("Where(depth != 0)", t => t.Where(nodeContext => nodeContext.Position.Depth != 0)),
      ("Select(upper)", t => t.Select(nodeContext => nodeContext.Node.ToUpperInvariant())),
      ("PruneBefore(== b)", t => t.PruneBefore(nodeContext => nodeContext.Node == "b")),
      ("PruneAfter(== b)", t => t.PruneAfter(nodeContext => nodeContext.Node == "b")),
      ("TakeNodesWhile(!= e)", t => t.TakeNodesWhile(nodeContext => nodeContext.Node != "e", false)),
      ("Union", t => t.Union(TreeSerializer.Deserialize("a(x,b(y))")).Select(nodeContext => nodeContext.Node.ToString())),
      ("RootfixScan(concat)", t => t.RootfixScan((accumulate, nodeContext) => accumulate.Node + nodeContext.Node, "*")),
      ("Invert", t => t.Invert()),
      ("Memoize", t => t.Memoize()),
      ("Materialize", t => t.Materialize()),
      ("Where+Select+Memoize chain", t => t.Where(nodeContext => nodeContext.Node != "c").Select(nodeContext => nodeContext.Node + "!").Memoize()),
    };

    [TestMethod]
    public void TreeOperatorsConformOverContractTree()
    {
      foreach (var tree in new[] { RichTree, RichForest })
      {
        foreach (var (name, op) in TreeOperators)
        {
          AssertSameStream(op(TreeSerializer.Deserialize(tree)), op(ContractTree.Parse(tree)), depthFirst: true, TraverseAll, $"{name} over {tree}");
          AssertSameStream(op(TreeSerializer.Deserialize(tree)), op(ContractTree.Parse(tree)), depthFirst: false, TraverseAll, $"{name} over {tree}");
        }
      }
    }

    [TestMethod]
    public void ValueOperatorsConformOverContractTree()
    {
      foreach (var tree in new[] { RichTree, RichForest })
      {
        var engine = TreeSerializer.Deserialize(tree);
        var contract = ContractTree.Parse(tree);

        Assert.AreEqual(engine.CountNodes(), contract.CountNodes(), $"CountNodes over {tree}");
        CollectionAssert.AreEqual(engine.PreOrderTraversal().ToArray(), contract.PreOrderTraversal().ToArray(), $"PreOrder over {tree}");
        CollectionAssert.AreEqual(engine.PostOrderTraversal().ToArray(), contract.PostOrderTraversal().ToArray(), $"PostOrder over {tree}");
        CollectionAssert.AreEqual(engine.LevelOrderTraversal().ToArray(), contract.LevelOrderTraversal().ToArray(), $"LevelOrder over {tree}");
        CollectionAssert.AreEqual(engine.GetLeaves().ToArray(), contract.GetLeaves().ToArray(), $"GetLeaves over {tree}");
        CollectionAssert.AreEqual(
          engine.GetLevels().Select(level => string.Join("|", level)).ToArray(),
          contract.GetLevels().Select(level => string.Join("|", level)).ToArray(),
          $"GetLevels over {tree}");
        Assert.AreEqual(engine.ToFormattedString(), contract.ToFormattedString(), $"ToFormattedString over {tree}");
        Assert.AreEqual(engine.Serialize(), contract.Serialize(), $"Serialize over {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // The pre-enumeration convention: a fresh treenumerator, before its first MoveNext, sits at
    // Position (0, -1) -- the virtual forest root -- with VisitCount 0 and Mode SchedulingNode.
    // Undocumented but load-bearing: WhereDFT seeds its sentinel from it and tests Depth == -1
    // as "not started"; WhereBFT, RootfixScan, StructuralMerge (its ForestRoot constant) and
    // EnumerableAsForestTreenumerator all lean on it. Asserted here for the concrete
    // implementations AND every operator's output -- wrappers that publish their own state can
    // each violate it independently.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PreEnumerationStateIsTheForestRoot()
    {
      foreach (var tree in Trees)
      {
        AssertPreEnumerationState(TreeSerializer.Deserialize(tree), $"engine '{tree}'");
        AssertPreEnumerationState(ContractTree.Parse(tree), $"contract '{tree}'");
      }

      foreach (var tree in new[] { RichTree, RichForest })
      {
        foreach (var (name, op) in TreeOperators)
        {
          AssertPreEnumerationState(op(TreeSerializer.Deserialize(tree)), $"{name} over engine '{tree}'");
          AssertPreEnumerationState(op(ContractTree.Parse(tree)), $"{name} over contract '{tree}'");
        }
      }
    }

    private static void AssertPreEnumerationState(ITreenumerable<string> tree, string context)
    {
      using (var depthFirst = tree.GetDepthFirstTreenumerator())
      using (var breadthFirst = tree.GetBreadthFirstTreenumerator())
      {
        Assert.AreEqual(NodePosition.ForestRoot, depthFirst.Position, $"[{context}] DFT pre-enumeration Position");
        Assert.AreEqual(0, depthFirst.VisitCount, $"[{context}] DFT pre-enumeration VisitCount");
        Assert.AreEqual(TreenumeratorMode.SchedulingNode, depthFirst.Mode, $"[{context}] DFT pre-enumeration Mode");

        Assert.AreEqual(NodePosition.ForestRoot, breadthFirst.Position, $"[{context}] BFT pre-enumeration Position");
        Assert.AreEqual(0, breadthFirst.VisitCount, $"[{context}] BFT pre-enumeration VisitCount");
        Assert.AreEqual(TreenumeratorMode.SchedulingNode, breadthFirst.Mode, $"[{context}] BFT pre-enumeration Mode");
      }
    }

    // ---------------------------------------------------------------------------------------
    // Lockstep stream comparison. The strategy for each MoveNext is computed from the visit
    // just emitted (asserted equal for both sides first, so both receive the same strategy).
    // ---------------------------------------------------------------------------------------

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
