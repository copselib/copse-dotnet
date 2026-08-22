using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The Walk adapter's conformance (Dag.FromTopology): the degenerate-tower pin -- walking the
  // buffer's own topology reproduces the buffer's visit stream EXACTLY (ordinals, modes, edges,
  // dispatch contiguity), and walking the builder's topology reproduces the builder's; a cyclic
  // topology streams its acyclic prefix and throws DagCycleException at starvation, naming the
  // loop; the strategies are honored (a severed arrival is a liveness vote); a listed source
  // another source reaches is a member, not a walk source.
  [TestClass]
  public class DagTopologyWalkTests
  {
    [TestMethod]
    public void WalkingTheBuffersTopology_ReproducesTheBuffersStream()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
      {
        var buffer = factory().Materialize();
        CollectionAssert.AreEqual(DagWalkerCorpus.Stream(buffer), DagWalkerCorpus.Stream(Dag.FromTopology(buffer.GetDagWalker().Topology)), dagName);
      }
    }

    [TestMethod]
    public void WalkingTheBuildersTopology_ReproducesTheBuildersStream()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
      {
        var builder = factory();
        CollectionAssert.AreEqual(DagWalkerCorpus.Stream(builder), DagWalkerCorpus.Stream(Dag.FromTopology(builder.GetDagWalker().Topology)), dagName);
      }
    }

    [TestMethod]
    public void TheBuildersTopology_SeesTheStrayParentRule()
    {
      // A member with a parent OUTSIDE the dag: the stray edge is not the dag's, so the walker's
      // in-edge group omits it, as the stream does.
      var stray = new DagNode<string, decimal>("stray");
      var source = new DagNode<string, decimal>("source");
      var member = source.AddChild("member", 1m);
      stray.AddChild(member, 9m);
      var dag = new Dag<string, decimal>(source);

      var walker = dag.GetDagWalkerAt(member);
      Assert.AreEqual("source", walker.MoveToParent(0).Value.GetValue());
      Assert.IsFalse(walker.MoveToParent(1).HasValue, "the stray parent is not a member");
      CollectionAssert.AreEqual(new[] { "source->member:1.00" }, DagWalkerCorpus.Edges(dag));
    }

    [TestMethod]
    public void ACyclicTopology_StreamsItsAcyclicPrefix_ThenNamesTheLoop()
    {
      var topology = new DictionaryTopology(
        sources: new[] { "a" },
        children: new Dictionary<string, string[]> { ["a"] = new[] { "b" }, ["b"] = new[] { "c" }, ["c"] = new[] { "b" } });

      var entered = new List<string>();
      var exception = Assert.ThrowsException<DagCycleException>(() =>
      {
        using var walk = Dag.FromTopology(topology).GetDagnumerator();
        while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
          if (walk.Mode == DagnumeratorMode.EnteringNode)
            entered.Add(walk.Node);
      });

      CollectionAssert.AreEqual(new[] { "a" }, entered, "the maximal acyclic prefix is published first");
      StringAssert.Contains(exception.Message, "Cycle detected: b -> c -> b");
    }

    [TestMethod]
    public void Strategies_AreHonored_SeveredArrivalsAreLivenessVotes()
    {
      var topology = DagWalkerCorpus.Diamond().Materialize().GetDagWalker().Topology;

      Assert.IsTrue(Entered(topology, visit => visit.node == "venture" && visit.edge == 0.70m ? DagTraversalStrategies.SkipEdge : DagTraversalStrategies.TraverseAll).Contains("venture"), "severing one arrival: the venture still enters via the other");
      Assert.IsFalse(Entered(topology, visit => visit.node == "venture" ? DagTraversalStrategies.SkipEdge : DagTraversalStrategies.TraverseAll).Contains("venture"), "severing every arrival: the venture never enters");
      CollectionAssert.AreEqual(new[] { "apex" }, Entered(topology, visit => visit.entering && visit.node == "apex" ? DagTraversalStrategies.SkipOutEdges : DagTraversalStrategies.TraverseAll), "suppressing the apex's departures starves everything below");
    }

    private static List<string> Entered(IDagTopology<string, int, decimal> topology, Func<(bool entering, string node, decimal edge), DagTraversalStrategies> verdict)
    {
      var entered = new List<string>();
      using var walk = Dag.FromTopology(topology).GetDagnumerator();
      var strategies = DagTraversalStrategies.TraverseAll;
      while (walk.MoveNext(strategies))
      {
        var entering = walk.Mode == DagnumeratorMode.EnteringNode;
        if (entering)
          entered.Add(walk.Node);
        strategies = verdict((entering, walk.Node, walk.Edge));
      }
      return entered;
    }

    [TestMethod]
    public void AListedSourceAnotherSourceReaches_IsAMemberNotAWalkSource()
    {
      var topology = new DictionaryTopology(
        sources: new[] { "a", "b" },
        children: new Dictionary<string, string[]> { ["a"] = new[] { "b" }, ["b"] = new string[0] });

      var dag = Dag.FromTopology(topology);
      CollectionAssert.AreEqual(new[] { "a" }, dag.GetSources().ToList());
      CollectionAssert.AreEqual(new[] { "a", "b" }, dag.GetTopologicalOrder().ToList());
    }

    // A test-owned topology over adjacency lists (in-edges derived), for shapes the builder
    // refuses to hold -- cycles -- and for source lists with overlap.
    private sealed class DictionaryTopology : IDagTopology<string, string, decimal>
    {
      public DictionaryTopology(string[] sources, Dictionary<string, string[]> children)
      {
        _Sources = sources;
        _Children = children;
        _Parents = children.Keys.ToDictionary(node => node, node => new List<string>());
        foreach (var pair in children)
          foreach (var child in pair.Value)
            _Parents[child].Add(pair.Key);
      }

      private readonly string[] _Sources;
      private readonly Dictionary<string, string[]> _Children;
      private readonly Dictionary<string, List<string>> _Parents;

      public string GetValue(string handle) => handle;

      public DagStep<string, decimal> TryGetParentAt(string handle, int inEdgeIndex)
        => inEdgeIndex >= 0 && inEdgeIndex < _Parents[handle].Count ? new DagStep<string, decimal>(_Parents[handle][inEdgeIndex], 1m, inEdgeIndex) : default;

      public DagStep<string, decimal> TryGetChildAt(string handle, int outEdgeIndex)
        => outEdgeIndex >= 0 && outEdgeIndex < _Children[handle].Length ? new DagStep<string, decimal>(_Children[handle][outEdgeIndex], 1m, outEdgeIndex) : default;

      public DagStep<string, decimal> TryGetSourceAt(int sourceIndex)
        => sourceIndex >= 0 && sourceIndex < _Sources.Length ? new DagStep<string, decimal>(_Sources[sourceIndex], default, sourceIndex) : default;
    }
  }
}
