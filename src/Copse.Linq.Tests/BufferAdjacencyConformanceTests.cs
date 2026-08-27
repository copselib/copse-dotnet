using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The adjacency-oracle battery (design-docs/WALKABLE_CONTRACT_DESIGN.md §4): after the buffer
  // re-parent, every capture answers the four walkable probes, and every answer must agree
  // with an INDEPENDENT model of the tree -- reconstructed here from the depth-first visit
  // stream's positions (the conformance-anchored truth, a different code path from the
  // adjacency engines) and re-addressed into whichever ordinal space the provider's layout
  // mints. One battery, every buffer producer rides it: declared captures in both layouts,
  // the organic dimension-dispatched capture (whose probe settles preorder), the walker
  // escalation alias, and the memo in its three tenses -- fresh (probes drive the growth:
  // the mid-race pull-through case), breadth-first-primed (level-order pin inherited), and
  // completed.
  [TestClass]
  public class BufferAdjacencyConformanceTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a,b(d),c(e(f))",
      "a(b(c(d(e))))",
      "a(b,c,d(e,f,g),h)",
    };

    private sealed class Provider
    {
      public Provider(string name, Func<ITreenumerable<string>, ITreeTopology<string, int>> create, BufferLayout handleSpace)
      {
        Name = name;
        Create = create;
        HandleSpace = handleSpace;
      }

      public string Name { get; }
      public Func<ITreenumerable<string>, ITreeTopology<string, int>> Create { get; }
      public BufferLayout HandleSpace { get; }
    }

    private static readonly Provider[] Providers =
    {
      new Provider("Materialize(Preorder)", tree => WalkerLawProviders.TopologyOf(tree.Materialize(BufferLayout.Preorder)), BufferLayout.Preorder),
      new Provider("Materialize(LevelOrder)", tree => WalkerLawProviders.TopologyOf(tree.Materialize(BufferLayout.LevelOrder)), BufferLayout.LevelOrder),
      new Provider("Materialize() organic, probe-settled", tree => WalkerLawProviders.TopologyOf(tree.Materialize()), BufferLayout.Preorder),
      new Provider("Memoize() fresh -- mid-race pull-through", tree => WalkerLawProviders.TopologyOf(tree.Memoize()), BufferLayout.Preorder),
      new Provider("Memoize() breadth-first-primed", tree =>
      {
        var memo = tree.Memoize();
        using (var pin = memo.GetBreadthFirstTreenumerator())
        {
          while (pin.MoveNext(NodeTraversalStrategies.TraverseAll))
          {
          }
        }
        return WalkerLawProviders.TopologyOf(memo);
      }, BufferLayout.LevelOrder),
      new Provider("Memoize() completed", tree =>
      {
        var memo = tree.Memoize();
        memo.Complete();
        return WalkerLawProviders.TopologyOf(memo);
      }, BufferLayout.Preorder),
    };

    [TestMethod]
    public void EveryBufferProducer_AgreesWithTheAdjacencyOracle()
    {
      foreach (var provider in Providers)
      {
        foreach (var tree in Trees)
        {
          var model = OracleModel.Build(tree);
          var ordinals = model.OrdinalsIn(provider.HandleSpace);
          var walkable = provider.Create(TreeSerializer.DeserializeDepthFirstTree(tree));
          var context = $"{provider.Name} [{tree}]";

          // Roots: every ordinal, then the first miss.
          for (var rootIndex = 0; rootIndex < model.Roots.Count; rootIndex++)
          {
            var rootResult = WalkerLawProviders.TopologyOf(walkable).TryGetRootAt(rootIndex);
            Assert.IsTrue(rootResult.HasValue, $"root {rootIndex} exists — {context}");
            Assert.AreEqual(ordinals[model.Roots[rootIndex]], rootResult.Value.Handle, $"root {rootIndex} — {context}");
            Assert.AreEqual(rootIndex, rootResult.Value.SiblingIndex, $"root {rootIndex} sibling — {context}");
          }

          Assert.IsFalse(WalkerLawProviders.TopologyOf(walkable).TryGetRootAt(model.Roots.Count).HasValue, $"past the last root — {context}");

          foreach (var node in model.Nodes)
          {
            var handle = ordinals[node];

            Assert.AreEqual(node.Value, WalkerLawProviders.TopologyOf(walkable).GetNode(handle), $"value @{handle} — {context}");

            var parentResult = WalkerLawProviders.TopologyOf(walkable).TryGetParent(handle);
            if (node.Parent == null)
            {
              Assert.IsFalse(parentResult.HasValue, $"root has no parent @{handle} — {context}");
            }
            else
            {
              Assert.IsTrue(parentResult.HasValue, $"parent exists @{handle} — {context}");
              Assert.AreEqual(ordinals[node.Parent], parentResult.Value, $"parent @{handle} — {context}");
            }

            for (var childIndex = 0; childIndex < node.Children.Count; childIndex++)
            {
              var childResult = WalkerLawProviders.TopologyOf(walkable).TryGetChildAt(handle, childIndex);
              Assert.IsTrue(childResult.HasValue, $"child {childIndex} exists @{handle} — {context}");
              Assert.AreEqual(ordinals[node.Children[childIndex]], childResult.Value.Handle, $"child {childIndex} @{handle} — {context}");
              Assert.AreEqual(childIndex, childResult.Value.SiblingIndex, $"child {childIndex} sibling @{handle} — {context}");
            }

            Assert.IsFalse(WalkerLawProviders.TopologyOf(walkable).TryGetChildAt(handle, node.Children.Count).HasValue, $"past the last child @{handle} — {context}");
          }
        }
      }
    }

    // ---------------------------------------------------------------------- the oracle

    private sealed class OracleNode
    {
      public string Value;
      public OracleNode Parent;
      public readonly List<OracleNode> Children = new List<OracleNode>();
    }

    private sealed class OracleModel
    {
      public readonly List<OracleNode> Nodes = new List<OracleNode>();
      public readonly List<OracleNode> Roots = new List<OracleNode>();

      // Reconstructed from the depth-first visit stream's positions -- scheduling events in
      // preorder, parents recovered from the depth stack. Independent of the probe engines.
      public static OracleModel Build(string tree)
      {
        var model = new OracleModel();
        var pathStack = new List<OracleNode>();

        using (var treenumerator = TreeSerializer.DeserializeDepthFirstTree(tree).GetDepthFirstTreenumerator())
        {
          while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          {
            if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
              continue;

            var depth = treenumerator.Position.Depth;
            pathStack.RemoveRange(depth, pathStack.Count - depth);

            var node = new OracleNode { Value = treenumerator.Node };

            if (depth == 0)
            {
              model.Roots.Add(node);
            }
            else
            {
              node.Parent = pathStack[depth - 1];
              node.Parent.Children.Add(node);
            }

            model.Nodes.Add(node);
            pathStack.Add(node);
          }
        }

        return model;
      }

      // Nodes arrive in preorder; the level-order space is a breadth-first sweep of the model.
      public Dictionary<OracleNode, int> OrdinalsIn(BufferLayout layout)
      {
        var ordinals = new Dictionary<OracleNode, int>();

        if (layout == BufferLayout.Preorder)
        {
          for (var index = 0; index < Nodes.Count; index++)
            ordinals[Nodes[index]] = index;

          return ordinals;
        }

        var frontier = new Queue<OracleNode>(Roots);
        var nextOrdinal = 0;

        while (frontier.Count > 0)
        {
          var node = frontier.Dequeue();
          ordinals[node] = nextOrdinal++;

          foreach (var child in node.Children)
            frontier.Enqueue(child);
        }

        return ordinals;
      }
    }
  }
}
