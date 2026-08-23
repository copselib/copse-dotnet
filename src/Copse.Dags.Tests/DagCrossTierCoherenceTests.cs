using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The coherence battery (the constitution alignment; the tree family's
  // CrossTierCoherenceTests, twinned): the laws every operator flavor must keep, pinned so
  // they never drift. Two laws:
  //
  //   1. THE NORTH STAR, n-ary form: a scan is the fold-shaped dispatch. The DAG encoding is
  //      SAME-NODE (the tree's target-node encoding is impossible under multiple parentage --
  //      no single survey holds a child's whole arrival), which is exactly why the DAG survey
  //      keeps its subject: at each family, compute the node's own accumulation from its
  //      arrivals and dispatch it down every edge. The scan's in-band empty boundary crosses
  //      into the seeded dispatch through the dispatcher-less filter (the noted seam): a
  //      source's virtual delivery is dispatcher-less, so "authored arrivals only" recovers
  //      the scan's empty-at-sources view.
  //   2. THE TRANSPOSE LAW: sinkfix IS sourcefix-of-the-transpose. The implementation reads
  //      the transpose without materializing it, so the law is pinned SEMANTICALLY here,
  //      through the literal Transpose() operator -- content-equivalent results, with the
  //      per-group order difference stated: direct sinkfix presents arrival groups in
  //      OUT-EDGE order (a structural fact); the literal transpose walk presents its
  //      in-adjacency in ITS discovery order. Order-insensitive folds see identical values.
  //
  // Every future boundary flavor joins this battery.
  [TestClass]
  public class DagCrossTierCoherenceTests
  {
    // A two-source mesh with a shared middle -- sources with different fan-outs, a diamond,
    // and a deep tail, so the laws are pinned off the happy path too.
    private static Dag<string, decimal> TwoSourceMesh()
    {
      var fundA = new DagNode<string, decimal>("fundA");
      var fundB = new DagNode<string, decimal>("fundB");
      var holdCo = fundA.AddChild("holdCo", 1.00m);
      var jv = holdCo.AddChild("jv", 0.60m);
      fundB.AddChild(jv, 0.40m);
      var opCo = jv.AddChild("opCo", 1.00m);
      holdCo.AddChild(opCo, 0.10m);
      opCo.AddChild("subCo", 1.00m);
      return new Dag<string, decimal>(fundA, fundB);
    }

    private static decimal EffectiveOwnership(string entity, IReadOnlyList<DagInflow<decimal, decimal>> inflows)
      => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge);

    // ---------------------------------------------------------------------------------------
    // Law 1: the north star, downward.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SourcefixScan_IsTheFoldShapedDispatch()
    {
      foreach (var dag in new[] { DagWalkerCorpus.Diamond(), TwoSourceMesh() })
      {
        var scan = dag.Sourcefix().Scan<decimal>(EffectiveOwnership);

        // The same-node fold encoding: each family's survey computes the subject's own
        // accumulation from its AUTHORED arrivals (the dispatcher-less filter recovers the
        // scan's empty-at-sources boundary) and dispatches it down every edge.
        var dispatch = dag.Sourcefix().Dispatch<decimal>(
          0m,
          (subject, arrivals, targets) =>
          {
            if (subject is null)
            {
              foreach (var target in targets)
                target.Dispatch(0m);
              return;
            }

            var authored = arrivals
              .Where(arrival => arrival.Dispatcher is not null)
              .Select(arrival => new DagInflow<decimal, decimal>(arrival.Value, arrival.Edge))
              .ToArray();

            var accumulation = EffectiveOwnership(subject, authored);
            foreach (var target in targets)
              target.Dispatch(accumulation);
          });

        // Every edge's delivery in the dispatch IS the parent's scan accumulation.
        var accumulateByName = scan.Values.ToDictionary(pair => pair.Node, pair => pair.Accumulate);
        var edges = dispatch.GetEdges().ToList();
        Assert.AreNotEqual(0, edges.Count);
        foreach (var edge in edges)
          Assert.AreEqual(
            accumulateByName[edge.Parent.Node],
            edge.Child.Arrivals[edge.InEdgeIndex],
            $"{edge.Parent.Node} -> {edge.Child.Node}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // Law 1: the north star, upward.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SinkfixScan_IsTheFoldShapedDispatch()
    {
      foreach (var dag in new[] { DagWalkerCorpus.Diamond(), TwoSourceMesh() })
      {
        var scan = dag.Sinkfix().Scan<decimal>(
          (entity, childResults) => 1m + childResults.Sum(result => result.Value * result.Edge));

        // Upward there is no boundary invocation and no filter to apply: sinks see empty
        // arrivals in both tiers, and each node dispatches its own accumulation up every
        // in-edge.
        var dispatch = dag.Sinkfix().Dispatch<decimal>(
          (subject, arrivals, targets) =>
          {
            var upflows = new DagInflow<decimal, decimal>[arrivals.Count];
            for (var index = 0; index < arrivals.Count; index++)
              upflows[index] = new DagInflow<decimal, decimal>(arrivals[index].Value, arrivals[index].Edge);

            var accumulation = 1m + upflows.Sum(upflow => upflow.Value * upflow.Edge);
            foreach (var target in targets)
              target.Dispatch(accumulation);
          });

        // Every node's upflow arrival from a child IS that child's scan accumulation, matched
        // INDEX-EXACT: upward arrival groups sit in out-edge order, and a discovery's
        // EdgeIndex is precisely the edge's index in the dispatching parent's out-edge list.
        var accumulateByName = scan.Values.ToDictionary(pair => pair.Node, pair => pair.Accumulate);
        var pairs = dispatch.Values;
        var edgesChecked = 0;

        using (var walk = dispatch.GetDagnumerator())
        {
          while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
          {
            if (walk.Mode != DagnumeratorMode.DiscoveringNode || walk.ParentOrdinal < 0)
              continue;

            var parent = pairs[walk.ParentOrdinal];
            var child = pairs[walk.Ordinal];
            Assert.AreEqual(
              accumulateByName[child.Node],
              parent.Arrivals[walk.EdgeIndex],
              $"{child.Node}'s accumulation must be {parent.Node}'s arrival on out-edge {walk.EdgeIndex}");
            edgesChecked++;
          }
        }

        Assert.AreNotEqual(0, edgesChecked);

        // And the sink boundary matches in-band: a node with no upflow arrivals is a sink,
        // and its scan value is the fold's own empty-list seed.
        var sinksSeen = 0;
        foreach (var pair in dispatch.Values)
        {
          if (pair.Arrivals.Count != 0)
            continue;
          Assert.AreEqual(1m, accumulateByName[pair.Node], $"{pair.Node} is a sink");
          sinksSeen++;
        }
        Assert.AreNotEqual(0, sinksSeen);
      }
    }

    // ---------------------------------------------------------------------------------------
    // Law 2: the transpose law.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SinkfixScan_IsSourcefixOfTheTranspose()
    {
      foreach (var dag in new[] { DagWalkerCorpus.Diamond(), TwoSourceMesh() })
      {
        // Order-insensitive fold (sum), because the two paths present arrival groups in
        // different per-group orders: direct sinkfix in OUT-EDGE order (structural), the
        // literal transpose walk in its own discovery order.
        var direct = dag.Sinkfix().Scan<decimal>(
          (entity, childResults) => 1m + childResults.Sum(result => result.Value * result.Edge));

        var viaTranspose = dag.Transpose().Sourcefix().Scan<decimal>(
          (entity, inflows) => 1m + inflows.Sum(inflow => inflow.Value * inflow.Edge));

        var directByName = direct.Values.ToDictionary(pair => pair.Node, pair => pair.Accumulate);
        var transposeByName = viaTranspose.Values.ToDictionary(pair => pair.Node, pair => pair.Accumulate);

        CollectionAssert.AreEquivalent(directByName.Keys, transposeByName.Keys);
        foreach (var name in directByName.Keys)
          Assert.AreEqual(directByName[name], transposeByName[name], name);

        // And the round trip: the transpose of the transpose is content-identical.
        var roundTrip = dag.Materialize().Transpose().Transpose();
        CollectionAssert.AreEqual(
          dag.Materialize().Values.ToArray(), roundTrip.Values.ToArray());
      }
    }

    // ---------------------------------------------------------------------------------------
    // The virtual source family: the seed is an allocation, not a broadcast.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void VirtualSourceFamily_AllocatesTheSeedAcrossSources()
    {
      // Budget-across-sources with the SAME callback that allocates everywhere else -- the
      // capability the manufactured per-source seed inflow could not express.
      var dispatched = TwoSourceMesh().Sourcefix().Dispatch<decimal>(
        900m,
        (subject, arrivals, targets) =>
        {
          var arrived = arrivals.Sum(arrival => arrival.Value);
          var totalWeight = targets.Sum(target => target.Edge);
          foreach (var target in targets)
            target.Dispatch(totalWeight == 0m ? arrived / targets.Count : arrived * target.Edge / totalWeight);
        });

      var byName = dispatched.Values.ToDictionary(pair => pair.Node, pair => pair);

      // The virtual family's targets carry default edges, so the allocator's even split ran:
      // two sources, 450 each -- one seed, allocated, not broadcast.
      Assert.AreEqual(450m, byName["fundA"].Arrivals[0]);
      Assert.AreEqual(450m, byName["fundB"].Arrivals[0]);

      // Conservation end to end: what the sinks hold is what the seed funded.
      var terminal = byName["subCo"].Arrivals.ToArray().Sum();
      Assert.AreEqual(900m, terminal);
    }

    [TestMethod]
    public void VirtualSourceFamily_SeedArrivalIsDispatcherless()
    {
      string virtualSubject = "unset";
      var dispatcherlessSubjects = new List<string>();
      var seenDispatchers = new List<string>();

      DagWalkerCorpus.Diamond().Sourcefix().Dispatch<decimal>(
        100m,
        (subject, arrivals, targets) =>
        {
          if (subject is null)
          {
            virtualSubject = null;
            Assert.AreEqual(1, arrivals.Count, "the virtual family's single arrival is the seed");
            Assert.AreEqual(100m, arrivals[0].Value);
            Assert.IsNull(arrivals[0].Dispatcher, "the seed has no author inside the dag");
          }
          else
          {
            foreach (var arrival in arrivals)
            {
              if (arrival.Dispatcher is null)
                dispatcherlessSubjects.Add(subject);
              else
                seenDispatchers.Add(arrival.Dispatcher);
            }
          }

          var arrived = arrivals.Sum(arrival => arrival.Value);
          var totalWeight = targets.Sum(target => target.Edge);
          foreach (var target in targets)
            target.Dispatch(totalWeight == 0m ? arrived / targets.Count : arrived * target.Edge / totalWeight);
        });

      Assert.IsNull(virtualSubject, "the virtual family fired");
      CollectionAssert.AreEqual(new[] { "apex" }, dispatcherlessSubjects,
        "a dispatcher-less arrival is the in-band arrived-from-outside test: exactly the source's virtual delivery");
      Assert.IsFalse(seenDispatchers.Contains(null));
      CollectionAssert.Contains(seenDispatchers, "apex");
    }

    // ---------------------------------------------------------------------------------------
    // Per-group order: a structural fact, pinned.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PerGroupOrder_DownwardDiscoveryOrder_UpwardOutEdgeOrder()
    {
      // Parallel edges with distinct payloads make order observable without comparing nodes.
      var top = new DagNode<string, decimal>("top");
      var mid = top.AddChild("mid", 0.25m);
      top.AddChild(mid, 0.75m);
      mid.AddChild("bottom", 1.00m);
      var dag = new Dag<string, decimal>(top);

      var downward = dag.Sourcefix().Scan<(string Name, decimal[] EdgeOrder)>(
        (entity, inflows) => (entity, inflows.Select(inflow => inflow.Edge).ToArray()));

      var upward = dag.Sinkfix().Scan<(string Name, decimal[] EdgeOrder)>(
        (entity, childResults) => (entity, childResults.Select(result => result.Edge).ToArray()));

      var mid2 = downward.Values.Single(pair => pair.Node == "mid").Accumulate;
      CollectionAssert.AreEqual(new[] { 0.25m, 0.75m }, mid2.EdgeOrder,
        "downward arrival groups sit in in-edge DISCOVERY order (= the parent's dispatch order)");

      var top2 = upward.Values.Single(pair => pair.Node == "top").Accumulate;
      CollectionAssert.AreEqual(new[] { 0.25m, 0.75m }, top2.EdgeOrder,
        "upward arrival groups sit in OUT-EDGE order -- a structural fact, not the walk's accidental linearization");
    }
  }
}
