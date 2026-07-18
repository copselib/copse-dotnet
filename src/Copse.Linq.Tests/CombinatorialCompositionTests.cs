using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The exhaustive composition gate: every tree in the combinatorial corpus x every ordered operator
  // chain (length 1..3 over the five composable operator kinds) x every filter-target node x consumer
  // strategy interference, each case comparing the COMPOSED pipeline against the same chain FORCED
  // TO STACK (Tree.Defer interposed between operators: the delegating wrapper is not composable, and
  // deferring a deferred tree is semantics-neutral). Composition's whole contract is that the two
  // are indistinguishable -- including under consumer skips, which exercise the composed driver's
  // per-node reject strategies against the stacked layers' independent ones.
  //
  // Mirrors CombinatorialWhereTests' one-in-process-loop shape (no DynamicData: MSTest would
  // enumerate the space during discovery). Note that suite's composed-Where arm already runs
  // COMPOSED Where chains against the materialized oracle (~891k cases per dimension); this suite
  // adds the mixed-operator and positional-flavor space against the stacked control.
  [TestClass]
  public class CombinatorialCompositionTests
  {
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void DepthFirstComposedMatchesStacked() => RunScan(TreeTraversalStrategy.DepthFirst);

    [TestMethod]
    public void BreadthFirstComposedMatchesStacked() => RunScan(TreeTraversalStrategy.BreadthFirst);

    // Filters and prunes aim at a target node by PREFIX, which keeps targets stable under the
    // tagging Selects (a node "b" stays matchable as "b*", "b*^", ...). The positional flavors
    // deliberately carry value-equivalent predicates: they exist to exercise the join/decline
    // machinery (a positional operator joins only a projections-only prefix), not to add
    // position-dependent semantics the stacked control would just mirror anyway.
    private delegate ITreenumerable<string> OperatorApplication(ITreenumerable<string> source, string targetNode);

    private static readonly (char Symbol, OperatorApplication Apply)[] OperatorAlphabet =
    {
      ('w', (source, targetNode) => source.Where(node => !node.StartsWith(targetNode))),
      ('W', (source, targetNode) => source.Where((node, position) => !node.StartsWith(targetNode))),
      ('p', (source, targetNode) => source.PruneBefore(node => node.StartsWith(targetNode))),
      ('P', (source, targetNode) => source.PruneBefore((node, position) => node.StartsWith(targetNode))),
      ('a', (source, targetNode) => source.PruneAfter(node => node.StartsWith(targetNode))),
      ('A', (source, targetNode) => source.PruneAfter((node, position) => node.StartsWith(targetNode))),
      ('s', (source, targetNode) => source.Select(node => node + "*")),
      ('S', (source, targetNode) => source.Select((node, position) => node + "^")),
    };

    // All seven non-trivial flag combinations (CombinatorialWhereTests' 7^k convention) --
    // SkipSiblings especially, the strategy with a bug-class history under promotion.
    private static readonly NodeTraversalStrategies[] ConsumerStrategies =
    {
      NodeTraversalStrategies.SkipNode,
      NodeTraversalStrategies.SkipDescendants,
      NodeTraversalStrategies.SkipSiblings,
      NodeTraversalStrategies.SkipNodeAndDescendants,
      NodeTraversalStrategies.SkipNodeAndSiblings,
      NodeTraversalStrategies.SkipDescendantsAndSiblings,
      NodeTraversalStrategies.SkipAll,
    };

    private void RunScan(TreeTraversalStrategy treeTraversalStrategy)
    {
      var deserializedByString = new Dictionary<string, ITreenumerable<string>>();
      ITreenumerable<string> Deserialize(string treeString)
      {
        if (!deserializedByString.TryGetValue(treeString, out var treenumerable))
        {
          treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);
          deserializedByString[treeString] = treenumerable;
        }
        return treenumerable;
      }

      var chains = Chains().ToArray();

      long total = 0;
      long failed = 0;
      var failures = new List<string>();

      foreach (var treeString in CombinatorialTestData.AllTreeStrings)
      {
        var nodes = treeString.Where(char.IsLetter).Select(character => character.ToString()).Distinct().ToArray();

        foreach (var chain in chains)
        {
          foreach (var targetNode in nodes)
          {
            // Consumer interference on chains of length <= 2; length-3 chains run without it
            // (the 8-symbol alphabet cubed times full interference would triple the runtime
            // for diminishing coverage -- the strategy machinery is chain-length-agnostic).
            foreach (var (strategyNode, consumerStrategy) in chain.Length <= 2 ? StrategyAssignments(nodes) : NoInterference)
            {
              total++;

              NodeTraversalStrategies Selector(NodeContext<string> nodeContext)
                => strategyNode != null && nodeContext.Node.StartsWith(strategyNode)
                  ? consumerStrategy
                  : NodeTraversalStrategies.TraverseAll;

              ITreenumerable<string> composedPipeline = Deserialize(treeString);
              foreach (var chainOperator in chain)
                composedPipeline = chainOperator.Apply(composedPipeline, targetNode);

              ITreenumerable<string> stackedPipeline = Deserialize(treeString);
              foreach (var chainOperator in chain)
              {
                var frozen = stackedPipeline;
                stackedPipeline = chainOperator.Apply(Tree.Defer(() => frozen), targetNode);
              }

              var expected = Key(stackedPipeline.GetTraversal(treeTraversalStrategy, Selector));
              // Take() bounds a hypothetical non-terminating regression into a length mismatch.
              var actual = Key(composedPipeline.GetTraversal(treeTraversalStrategy, Selector)).Take(100_000);

              if (!expected.SequenceEqual(actual))
              {
                failed++;
                if (failures.Count < 40)
                  failures.Add($"{treeString} chain={new string(chain.Select(chainOperator => chainOperator.Symbol).ToArray())} target={targetNode} consumer={(strategyNode == null ? "none" : $"{strategyNode}:{consumerStrategy}")}");
              }
            }
          }
        }
      }

      TestContext.WriteLine($"CombinatorialCompositionTests ({treeTraversalStrategy}): {total} cases across {CombinatorialTestData.AllTreeStrings.Length} trees x {chains.Length} chains.");

      Assert.AreEqual(
        0L,
        failed,
        $"{treeTraversalStrategy} composed pipeline diverged from the stacked control on {failed} of {total} cases:{Environment.NewLine}"
        + string.Join(Environment.NewLine, failures));
    }

    private static readonly (string Node, NodeTraversalStrategies Strategy)[] NoInterference =
    {
      (null, NodeTraversalStrategies.TraverseAll),
    };

    // No interference, plus one (node, strategy) assignment per node x strategy.
    private static IEnumerable<(string Node, NodeTraversalStrategies Strategy)> StrategyAssignments(string[] nodes)
    {
      yield return (null, NodeTraversalStrategies.TraverseAll);

      foreach (var node in nodes)
        foreach (var consumerStrategy in ConsumerStrategies)
          yield return (node, consumerStrategy);
    }

    private static IEnumerable<(char Symbol, OperatorApplication Apply)[]> Chains()
    {
      foreach (var first in OperatorAlphabet)
      {
        yield return new[] { first };

        foreach (var second in OperatorAlphabet)
        {
          yield return new[] { first, second };

          foreach (var third in OperatorAlphabet)
            yield return new[] { first, second, third };
        }
      }
    }

    private static IEnumerable<(TreenumeratorMode, int, int, int, string)> Key(IEnumerable<NodeVisit<string>> visits) =>
      visits.Select(visit => (visit.Mode, visit.Position.Depth, visit.Position.SiblingIndex, visit.VisitCount, visit.Node));
  }
}
