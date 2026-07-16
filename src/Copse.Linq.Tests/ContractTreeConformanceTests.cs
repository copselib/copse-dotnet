using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The engine-free contract tests. ContractTree implements ITreenumerable directly (no engine,
  // no child enumerators); these tests prove (a) it conforms to the engine's visit-stream
  // contract under every traversal strategy, and (b) the operator suite behaves identically
  // over it -- i.e. operators depend only on the abstract contract, so ANY conforming
  // implementation works. (b) is the enforcement test for the package-architecture principle
  // that Linq's independence from the engine is semantic (see PACKAGE_ARCHITECTURE.md).
  // Thin adapter over VisitStreamConformance (the shared visit-contract battery).
  [TestClass]
  public class ContractTreeConformanceTests
  {
    private const string RichTree = "a(b(d,e,f),c(g,h,i))";
    private const string RichForest = "a(d(g)),b(e(h)),c(f(i))";

    // ---------------------------------------------------------------------------------------
    // Direct conformance: ContractTree's visit streams are identical to the engine's.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void TraverseAll_MatchesEngine()
    {
      VisitStreamConformance.AssertTraverseAllConforms(tree => ContractTree.Parse(tree).GetDepthFirstTreenumerator(), depthFirst: true, "contract");
      VisitStreamConformance.AssertTraverseAllConforms(tree => ContractTree.Parse(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "contract");
    }

    [TestMethod]
    public void EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => ContractTree.Parse(tree).GetDepthFirstTreenumerator(), depthFirst: true, "contract");

    [TestMethod]
    public void EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => ContractTree.Parse(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "contract");

    [TestMethod]
    public void StrategiesOnVisitingVisitsAreIgnored()
    {
      // Both implementations must ignore flags passed while positioned on a visiting visit;
      // feed deterministic junk on every visiting visit and require identical streams.
      NodeTraversalStrategies Script(TreenumeratorMode mode, string node, int visitCount)
        => mode == TreenumeratorMode.VisitingNode
          ? VisitStreamConformance.SchedulingStrategies[(node[0] + visitCount) % VisitStreamConformance.SchedulingStrategies.Length]
          : NodeTraversalStrategies.TraverseAll;

      foreach (var tree in VisitStreamConformance.TreeCorpus.Where(t => t.Length > 0))
      {
        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: true), ContractTree.Parse(tree).GetDepthFirstTreenumerator(), Script, $"contract {tree}");
        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: false), ContractTree.Parse(tree).GetBreadthFirstTreenumerator(), Script, $"contract {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // Operator conformance: every operator produces identical results over the engine-backed
    // tree and the engine-free tree. This is the semantic-independence enforcement test.
    // ---------------------------------------------------------------------------------------

    private static readonly (string Name, Func<ITreenumerable<string>, ITreenumerable<string>> Op)[] TreeOperators =
    {
      ("Where(!= b)", t => t.Where(n => n != "b")),
      ("Where(depth != 0)", t => t.Where((n, position) => position.Depth != 0)),
      ("Select(upper)", t => t.Select(n => n.ToUpperInvariant())),
      ("PruneBefore(== b)", t => t.PruneBefore(n => n == "b")),
      ("PruneAfter(== b)", t => t.PruneAfter(n => n == "b")),
      ("TakeNodesWhile(!= e)", t => t.TakeNodesWhile(nodeContext => nodeContext.Node != "e", false)),
      ("Union", t => t.Union(EngineTree.Parse("a(x,b(y))")).Select(n => n.ToString())),
      ("RootfixScan(concat)", t => t.RootfixScan((accumulate, nodeContext) => accumulate.Node + nodeContext.Node, "*")),
      ("Invert+Memoize", t => t.Invert().Memoize()),
      ("Memoize", t => t.Memoize()),
      ("Materialize", t => t.Materialize()),
      ("Where+Select+Memoize chain", t => t.Where(n => n != "c").Select(n => n + "!").Memoize()),
    };

    [TestMethod]
    public void TreeOperatorsConformOverContractTree()
    {
      foreach (var tree in new[] { RichTree, RichForest })
      {
        foreach (var (name, op) in TreeOperators)
        {
          VisitStreamConformance.AssertSameStream(
            op(EngineTree.Parse(tree)).GetDepthFirstTreenumerator(),
            op(ContractTree.Parse(tree)).GetDepthFirstTreenumerator(),
            VisitStreamConformance.TraverseAll,
            $"{name} over {tree} DFT");
          VisitStreamConformance.AssertSameStream(
            op(EngineTree.Parse(tree)).GetBreadthFirstTreenumerator(),
            op(ContractTree.Parse(tree)).GetBreadthFirstTreenumerator(),
            VisitStreamConformance.TraverseAll,
            $"{name} over {tree} BFT");
        }
      }
    }

    [TestMethod]
    public void ValueOperatorsConformOverContractTree()
    {
      foreach (var tree in new[] { RichTree, RichForest })
      {
        var engine = EngineTree.Parse(tree);
        var contract = ContractTree.Parse(tree);

        Assert.AreEqual(engine.CountNodes(), contract.CountNodes(), $"CountNodes over {tree}");
        CollectionAssert.AreEqual(engine.PreorderTraversal().ToArray(), contract.PreorderTraversal().ToArray(), $"Preorder over {tree}");
        CollectionAssert.AreEqual(engine.PostorderTraversal().ToArray(), contract.PostorderTraversal().ToArray(), $"Postorder over {tree}");
        CollectionAssert.AreEqual(engine.LevelOrderTraversal().ToArray(), contract.LevelOrderTraversal().ToArray(), $"LevelOrder over {tree}");
        CollectionAssert.AreEqual(engine.GetLeaves().ToArray(), contract.GetLeaves().ToArray(), $"GetLeaves over {tree}");
        CollectionAssert.AreEqual(
          engine.GetLevels().Select(level => string.Join("|", level)).ToArray(),
          contract.GetLevels().Select(level => string.Join("|", level)).ToArray(),
          $"GetLevels over {tree}");
        Assert.AreEqual(engine.ToFormattedString(), contract.ToFormattedString(), $"ToFormattedString over {tree}");
        Assert.AreEqual(engine.SerializeDepthFirstTree(), contract.SerializeDepthFirstTree(), $"Serialize over {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // The pre-enumeration convention (NodePosition.ForestRoot): asserted for the concrete
    // implementations AND every operator's output -- wrappers that publish their own state can
    // each violate it independently.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PreEnumerationStateIsTheForestRoot()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(EngineTree.Parse(tree).GetDepthFirstTreenumerator(), $"engine '{tree}' DFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(EngineTree.Parse(tree).GetBreadthFirstTreenumerator(), $"engine '{tree}' BFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(ContractTree.Parse(tree).GetDepthFirstTreenumerator(), $"contract '{tree}' DFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(ContractTree.Parse(tree).GetBreadthFirstTreenumerator(), $"contract '{tree}' BFT");
      }

      foreach (var tree in new[] { RichTree, RichForest })
      {
        foreach (var (name, op) in TreeOperators)
        {
          VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(op(EngineTree.Parse(tree)).GetDepthFirstTreenumerator(), $"{name} over engine '{tree}' DFT");
          VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(op(EngineTree.Parse(tree)).GetBreadthFirstTreenumerator(), $"{name} over engine '{tree}' BFT");
          VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(op(ContractTree.Parse(tree)).GetDepthFirstTreenumerator(), $"{name} over contract '{tree}' DFT");
          VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(op(ContractTree.Parse(tree)).GetBreadthFirstTreenumerator(), $"{name} over contract '{tree}' BFT");
        }
      }
    }
  }
}
