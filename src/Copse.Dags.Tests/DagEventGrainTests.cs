using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The event-grain relabels -- SelectNodes over the event, SelectInEdges, SelectOutEdges,
  // PruneInEdges, PruneOutEdges -- and their places in the algebra, pinned: the out-edge pair
  // ARE the bind (Return answering Rewrite / Suppress), the in-edge pair are the
  // transpose-conjugates of the out-edge pair, the stateless edge operators are the group-aware
  // ones ignoring the group, the event-grain node projection ignoring its groups is the value
  // projection, and SelectNodes over the event IS the walker's Extend with a one-hop
  // observation (the extend half of the algebra, visibly the same seats). Then the receipt from
  // the work PoC: the reallocation policy as SelectInEdges, equal to its dispatch spelling.
  [TestClass]
  public class DagEventGrainTests
  {
    private static IEnumerable<(string Name, Func<Dag<string, decimal>> Factory)> Corpus()
      => DagWalkerCorpus.All().Concat(new[] { ("parallel", (Func<Dag<string, decimal>>)DagWalkerCorpus.ParallelEdges) });

    // A group-aware rewrite: each edge's payload scaled by the size of the group it sits in.
    private static IReadOnlyList<decimal> ScaledByGroup(IReadOnlyList<DagEdgeContext<string, decimal>> group)
      => group.Select(edge => edge.Edge * group.Count).ToList();

    // A group-aware verdict: prune the smallest edge of any group with two or more.
    private static IReadOnlyList<bool> PruneSmallestOfMany(IReadOnlyList<DagEdgeContext<string, decimal>> group)
    {
      var smallest = group.Count >= 2 ? group.Min(edge => edge.Edge) : decimal.MinValue;
      return group.Select(edge => group.Count >= 2 && edge.Edge == smallest).ToList();
    }

    [TestMethod]
    public void SelectOutEdges_IsTheBindOnTheSubdivision_RewritingEdgeElements()
    {
      // The bind owns no edge payloads on the node carrier (it would have to answer for an edge
      // after a promotion beneath it composed a suffix it cannot see -- the fragment theorem);
      // the edge grain is the bind on the SUBDIVISION, where an edge is a node and there is no
      // composer. The group-aware rewrite is reached by seats built test-side.
      foreach (var (name, factory) in Corpus())
      {
        var buffer = factory().Materialize();
        DagEventSeatsForTests.Build(buffer, out _, out var departures);
        var byValue = buffer.GetTopologicalOrder().Select((value, ordinal) => (value, ordinal)).ToDictionary(pair => pair.value, pair => pair.ordinal);

        Assert.AreEqual(
          DagWalkerCorpus.Content(buffer.SelectOutEdges((arrivals, node, outgoing) => ScaledByGroup(outgoing))),
          DagWalkerCorpus.Content(buffer.Subdivide()
            .SelectMany(element => element.IsEdge
              ? DagExpansion<DagElement<string, decimal>, Unit>.Return(DagElement<string, decimal>.OfEdge(new DagEdgeContext<string, decimal>(element.Edge.Parent, element.Edge.Child, ScaledByGroup(departures[byValue[element.Edge.Parent]])[IndexAmong(departures[byValue[element.Edge.Parent]], element.Edge)], element.Edge.InEdgeIndex)))
              : DagExpansion<DagElement<string, decimal>, Unit>.Return(element), Unit.Compose)
            .Unsubdivide()),
          name);
      }
    }

    [TestMethod]
    public void PruneOutEdges_IsTheBindOnTheSubdivision_DroppingEdgeElements()
    {
      foreach (var (name, factory) in Corpus())
      {
        var buffer = factory().Materialize();
        DagEventSeatsForTests.Build(buffer, out _, out var departures);
        var byValue = buffer.GetTopologicalOrder().Select((value, ordinal) => (value, ordinal)).ToDictionary(pair => pair.value, pair => pair.ordinal);

        Assert.AreEqual(
          DagWalkerCorpus.Content(buffer.PruneOutEdges((arrivals, node, outgoing) => PruneSmallestOfMany(outgoing))),
          DagWalkerCorpus.Content(buffer.Subdivide()
            .SelectMany(element => element.IsEdge && PruneSmallestOfMany(departures[byValue[element.Edge.Parent]])[IndexAmong(departures[byValue[element.Edge.Parent]], element.Edge)]
              ? DagExpansion<DagElement<string, decimal>, Unit>.Drop
              : DagExpansion<DagElement<string, decimal>, Unit>.Return(element), Unit.Compose)
            .Unsubdivide()),
          name);
      }
    }

    // The position of an edge within its parent's departure group (child + in-edge index identifies it).
    private static int IndexAmong(DagEdgeContext<string, decimal>[] departures, DagEdgeContext<string, decimal> edge)
      => Array.FindIndex(departures, departure => departure.Child == edge.Child && departure.InEdgeIndex == edge.InEdgeIndex);

    [TestMethod]
    public void InEdgeOperators_AreTheTransposeConjugates()
    {
      foreach (var (name, factory) in Corpus())
      {
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().SelectInEdges((arrivals, node, departures) => ScaledByGroup(arrivals))),
          DagWalkerCorpus.Content(factory().Transpose().SelectOutEdges((arrivals, node, departures) => ScaledByGroup(departures)).Transpose()),
          $"SelectInEdges = Transpose ∘ SelectOutEdges ∘ Transpose [{name}]");

        // The prunes: the SAME kept edge set gives the same result whichever seat decides it --
        // forward liveness settles the nodes either way.
        var buffer = factory().Materialize();
        var prunedFromChildren = new HashSet<string>(buffer.GetEdges().Where(edge => PruneSmallestOfMany(buffer.GetEdges().Where(other => other.Child == edge.Child).OrderBy(other => other.InEdgeIndex).ToList())[edge.InEdgeIndex]).Select(edge => $"{edge.Parent}->{edge.Child}#{edge.InEdgeIndex}"));
        Assert.AreEqual(
          DagWalkerCorpus.Content(buffer.PruneInEdges((arrivals, node, departures) => PruneSmallestOfMany(arrivals))),
          DagWalkerCorpus.Content(buffer.PruneOutEdges((arrivals, node, departures) => departures.Select(edge => prunedFromChildren.Contains($"{edge.Parent}->{edge.Child}#{edge.InEdgeIndex}")).ToList())),
          $"PruneInEdges and PruneOutEdges agree on the same kept set [{name}]");
      }
    }

    [TestMethod]
    public void StatelessEdgeOperators_AreTheGroupAwareOnesIgnoringTheGroup()
    {
      foreach (var (name, factory) in Corpus())
      {
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().SelectEdges(context => context.Edge * 2)),
          DagWalkerCorpus.Content(factory().SelectOutEdges((arrivals, node, departures) => departures.Select(edge => edge.Edge * 2).ToList())),
          $"SelectEdges = SelectOutEdges ignoring the group [{name}]");
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().SelectEdges(context => context.Edge * 2)),
          DagWalkerCorpus.Content(factory().SelectInEdges((arrivals, node, departures) => arrivals.Select(edge => edge.Edge * 2).ToList())),
          $"SelectEdges = SelectInEdges ignoring the group [{name}]");
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().PruneEdges(context => context.Edge == 0.70m || context.Edge == 0.3m)),
          DagWalkerCorpus.Content(factory().PruneOutEdges((arrivals, node, departures) => departures.Select(edge => edge.Edge == 0.70m || edge.Edge == 0.3m).ToList())),
          $"PruneEdges = PruneOutEdges ignoring the group [{name}]");
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().PruneEdges(context => context.Edge == 0.70m || context.Edge == 0.3m)),
          DagWalkerCorpus.Content(factory().PruneInEdges((arrivals, node, departures) => arrivals.Select(edge => edge.Edge == 0.70m || edge.Edge == 0.3m).ToList())),
          $"PruneEdges = PruneInEdges ignoring the group [{name}]");
      }
    }

    [TestMethod]
    public void SelectNodesOverTheEvent_IgnoringTheGroups_IsTheValueProjection()
    {
      foreach (var (name, factory) in Corpus())
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().SelectNodes(node => node.ToUpperInvariant())),
          DagWalkerCorpus.Content(factory().SelectNodes((arrivals, node, departures) => node.ToUpperInvariant())),
          name);
    }

    [TestMethod]
    public void SelectNodesOverTheEvent_IsTheWalkersExtend_WithAOneHopObservation()
    {
      // The extend half, on the same seats: the event-grain projection reads what the walker
      // sees one step away -- in-degree, out-degree, the arrivals' payload sum.
      foreach (var (name, factory) in Corpus())
      {
        var viaEvent = factory().SelectNodes((arrivals, node, departures) => $"{node}:{arrivals.Count}/{departures.Count}/{arrivals.Sum(edge => edge.Edge):0.00}");

        var buffer = factory().Materialize();
        var viaExtend = buffer.Extend((topology, handle) =>
        {
          var inDegree = 0;
          var arrivalsSum = 0m;
          for (var step = topology.TryGetParentAt(handle, 0); step.HasValue; step = topology.TryGetParentAt(handle, step.EdgeIndex + 1))
          {
            inDegree++;
            arrivalsSum += step.Edge;
          }
          var outDegree = 0;
          while (topology.TryGetChildAt(handle, outDegree).HasValue)
            outDegree++;
          return $"{topology.GetValue(handle)}:{inDegree}/{outDegree}/{arrivalsSum:0.00}";
        });

        CollectionAssert.AreEquivalent(viaEvent.GetTopologicalOrder().ToList(), viaExtend.GetTopologicalOrder().ToList(), name);
      }
    }

    [TestMethod]
    public void PruneConjugacy_IsAPrincipledNonLaw_LivenessIsOrientationBound()
    {
      // Prune is TEMPORAL (the prune clause): liveness runs in the walk's orientation. Pruning
      // the venture's smaller in-edge from the child's seat keeps `right` -- it is reachable
      // from the apex. The same cut on the TRANSPOSE makes `right` a node with no live
      // in-edge in that orientation, so it dies there and is gone when transposed back. The
      // Select pair is conjugate (no liveness); the Prune pair is not, by ruling.
      var fromTheChild = DagWalkerCorpus.Diamond().PruneInEdges((arrivals, node, departures) => PruneSmallestOfMany(arrivals));
      var viaTheTranspose = DagWalkerCorpus.Diamond().Transpose().PruneOutEdges((arrivals, node, departures) => PruneSmallestOfMany(departures)).Transpose();

      Assert.AreEqual("nodes[apex,left,right,venture] edges[apex->left:0.60,apex->right:0.40,left->venture:0.70] sources[apex]", DagWalkerCorpus.Content(fromTheChild));
      Assert.AreEqual("nodes[apex,left,venture] edges[apex->left:0.60,left->venture:0.70] sources[apex]", DagWalkerCorpus.Content(viaTheTranspose));
    }

    [TestMethod]
    public void TheReceipt_ReallocationPolicy_AsSelectInEdges_EqualsItsDispatchSpelling()
    {
      // The work PoC's ApplyReallocationPolicy: at every owned entity, zero the excluded owners'
      // fractions and redistribute them per capita over the survivors. Once a survey with
      // exactly-once Dispatch slots and a pairing to unwrap; now one group-aware projection.
      foreach (var (name, factory) in new[] { ("ownership", (Func<Dag<string, decimal>>)OwnershipWithGp), ("sharedLeaf", DagWalkerCorpus.SharedLeaf) })
      {
        Func<string, bool> excluded = owner => owner == "gp";

        var viaDispatch = factory()
          .Sinkfix().DispatchEdges<decimal>((entity, arrivals, owners) =>
          {
            var excludedOwners = owners.Where(owner => excluded(owner.Value)).ToList();
            var split = owners.Count - excludedOwners.Count == 0 ? 0m : excludedOwners.Sum(owner => owner.Edge) / (owners.Count - excludedOwners.Count);
            foreach (var owner in owners)
              owner.Dispatch(excluded(owner.Value) ? 0m : owner.Edge + split);
          })
          .SelectEdges(context => context.Edge.Accumulate);

        var viaEvent = factory().SelectInEdges((owners, entity, departures) =>
        {
          var excludedCount = owners.Count(owner => excluded(owner.Parent));
          var split = owners.Count - excludedCount == 0 ? 0m : owners.Where(owner => excluded(owner.Parent)).Sum(owner => owner.Edge) / (owners.Count - excludedCount);
          return owners.Select(owner => excluded(owner.Parent) ? 0m : owner.Edge + split).ToList();
        });

        Assert.AreEqual(DagWalkerCorpus.Content(viaDispatch), DagWalkerCorpus.Content(viaEvent), name);
      }

      // And the numbers, by hand: gp's 0.2% moves to the one survivor.
      var conditioned = OwnershipWithGp().SelectInEdges((owners, entity, departures) =>
      {
        var gpFraction = owners.Where(owner => owner.Parent == "gp").Sum(owner => owner.Edge);
        var survivors = owners.Count(owner => owner.Parent != "gp");
        return owners.Select(owner => owner.Parent == "gp" ? 0m : owner.Edge + gpFraction / survivors).ToList();
      });
      CollectionAssert.AreEqual(new[] { "fund->blue:1.00", "gp->blue:0.00" }, DagWalkerCorpus.Edges(conditioned));
    }

    // FSSP VI Blue: gp 0.2%, the fund 99.8%.
    private static Dag<string, decimal> OwnershipWithGp()
    {
      var gp = new DagNode<string, decimal>("gp");
      var fund = new DagNode<string, decimal>("fund");
      var blue = new DagNode<string, decimal>("blue");
      gp.AddChild(blue, 0.002m);
      fund.AddChild(blue, 0.998m);
      return new Dag<string, decimal>(gp, fund);
    }
  }

  // The seats, test-side, through the public surface (GetEdges), for the derivation pins that
  // need a node's departures by value before the bind runs.
  internal static class DagEventSeatsForTests
  {
    public static void Build(DagBuffer<string, decimal> buffer, out DagEdgeContext<string, decimal>[][] arrivals, out DagEdgeContext<string, decimal>[][] departures)
    {
      var order = buffer.GetTopologicalOrder().ToList();
      var ordinalOf = order.Select((value, ordinal) => (value, ordinal)).ToDictionary(pair => pair.value, pair => pair.ordinal);
      var edges = buffer.GetEdges().ToList();
      arrivals = order.Select(value => edges.Where(edge => edge.Child == value).OrderBy(edge => edge.InEdgeIndex).ToArray()).ToArray();
      departures = order.Select(value => edges.Where(edge => edge.Parent == value).ToArray()).ToArray();
    }
  }
}
