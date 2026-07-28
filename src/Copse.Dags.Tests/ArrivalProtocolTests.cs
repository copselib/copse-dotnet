using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The arrival protocol's birth pins (docs/DAG_CONTRACT_DESIGN.md, the arrival protocol,
  // phase 1): the grouped event stream exactly on the diamond, the bijection anchors (event
  // order == the visit protocol's entry order; flattened departures == GetEdges), the verdict
  // dialogue (suppress starves exclusive reach, shared targets survive via the other path,
  // severing ALL arrivals voids the departures but cannot retract the witnessed event), the
  // layered liveness fold, ordinal-gap preservation over wrapped sources, and the strict
  // ethos on out-of-dialogue and out-of-range verdicts.
  [TestClass]
  public class ArrivalProtocolTests
  {
    private static (string Node, int Ordinal, decimal Edge) Arr(DagArrival<string, decimal> arrival)
      => (arrival.Dispatcher, arrival.DispatcherOrdinal, arrival.Edge);

    private static (string Node, int Ordinal, decimal Edge) Dep(DagDeparture<string, decimal> departure)
      => (departure.Target, departure.TargetOrdinal, departure.Edge);

    private static List<DagNodeEvent<string, decimal>> Drain(
      IArrivalDagnumerator<string, decimal> events,
      Action<DagNodeEvent<string, decimal>, IArrivalDagnumerator<string, decimal>> verdicts = null)
    {
      var witnessed = new List<DagNodeEvent<string, decimal>>();

      while (events.MoveNext())
      {
        witnessed.Add(events.Current);
        verdicts?.Invoke(events.Current, events);
      }

      return witnessed;
    }

    // The ownership diamond: apex owns left 60% / right 40%; each owns the venture (70%/30%).
    private static Dag<string, decimal> Diamond()
    {
      var apex = new DagNode<string, decimal>("apex");
      var left = apex.AddChild("left", 0.60m);
      var right = apex.AddChild("right", 0.40m);
      var venture = new DagNode<string, decimal>("venture");
      left.AddChild(venture, 0.70m);
      right.AddChild(venture, 0.30m);
      return new Dag<string, decimal>(apex);
    }

    // The diamond with a tail: venture wholly owns holdco.
    private static Dag<string, decimal> DiamondWithTail()
    {
      var apex = new DagNode<string, decimal>("apex");
      var left = apex.AddChild("left", 0.60m);
      var right = apex.AddChild("right", 0.40m);
      var venture = new DagNode<string, decimal>("venture");
      left.AddChild(venture, 0.70m);
      right.AddChild(venture, 0.30m);
      venture.AddChild("holdco", 1.00m);
      return new Dag<string, decimal>(apex);
    }

    private static Dag<string, decimal> Chain()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m).AddChild("c", 1m);
      return new Dag<string, decimal>(a);
    }

    [TestMethod]
    public void Diamond_EventStream_IsPinned()
    {
      using var events = Diamond().GetArrivalDagnumerator();
      var witnessed = Drain(events);

      Assert.AreEqual(4, witnessed.Count);

      var apex = witnessed[0];
      Assert.AreEqual((0, "apex", true, false), (apex.Ordinal, apex.Value, apex.IsSource, apex.IsSink));
      Assert.AreEqual(0, apex.Arrivals.Count);
      CollectionAssert.AreEqual(
        new[] { ("left", 1, 0.60m), ("right", 2, 0.40m) },
        apex.Departures.Select(Dep).ToList());

      var left = witnessed[1];
      Assert.AreEqual((1, "left", false, false), (left.Ordinal, left.Value, left.IsSource, left.IsSink));
      CollectionAssert.AreEqual(new[] { ("apex", 0, 0.60m) }, left.Arrivals.Select(Arr).ToList());
      CollectionAssert.AreEqual(new[] { ("venture", 3, 0.70m) }, left.Departures.Select(Dep).ToList());

      var right = witnessed[2];
      Assert.AreEqual((2, "right", false, false), (right.Ordinal, right.Value, right.IsSource, right.IsSink));
      CollectionAssert.AreEqual(new[] { ("apex", 0, 0.40m) }, right.Arrivals.Select(Arr).ToList());
      CollectionAssert.AreEqual(new[] { ("venture", 3, 0.30m) }, right.Departures.Select(Dep).ToList());

      var venture = witnessed[3];
      Assert.AreEqual((3, "venture", false, true), (venture.Ordinal, venture.Value, venture.IsSource, venture.IsSink));
      CollectionAssert.AreEqual(
        new[] { ("left", 1, 0.70m), ("right", 2, 0.30m) },
        venture.Arrivals.Select(Arr).ToList());
    }

    [TestMethod]
    public void EventOrder_IsTheVisitProtocolEntryOrder()
    {
      // The bijection's first anchor: same nodes, same ordinals, same order as the entries.
      using var events = DiamondWithTail().GetArrivalDagnumerator();
      var eventOrdinals = Drain(events).Select(nodeEvent => nodeEvent.Ordinal).ToList();

      var entryOrdinals = new List<int>();
      using var dagnumerator = DiamondWithTail().GetForwardDagnumerator();
      while (dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll))
        if (dagnumerator.Mode == DagnumeratorMode.EnteringNode)
          entryOrdinals.Add(dagnumerator.Ordinal);

      CollectionAssert.AreEqual(entryOrdinals, eventOrdinals);
    }

    [TestMethod]
    public void FlattenedDepartures_AreExactlyGetEdges()
    {
      // The bijection's second anchor, constructive: exploding the grouped stream recovers
      // the flat edge stream -- grouped -> flat is the free direction.
      using var events = DiamondWithTail().GetArrivalDagnumerator();
      var exploded = Drain(events)
        .SelectMany(nodeEvent => nodeEvent.Departures.Select(departure => (nodeEvent.Value, departure.Target, departure.Edge)))
        .ToList();

      var edges = DiamondWithTail().GetEdges()
        .Select(edgeContext => (edgeContext.Parent, edgeContext.Child, edgeContext.Edge))
        .ToList();

      CollectionAssert.AreEqual(edges, exploded);
    }

    [TestMethod]
    public void SuppressDeparture_StarvesExclusivelyReachedTargets()
    {
      using var events = Chain().GetArrivalDagnumerator();

      var witnessed = Drain(events, (nodeEvent, walk) =>
      {
        if (nodeEvent.Value == "b")
          walk.SuppressDeparture(0);
      });

      CollectionAssert.AreEqual(new[] { "a", "b" }, witnessed.Select(nodeEvent => nodeEvent.Value).ToList());
    }

    [TestMethod]
    public void SuppressDeparture_SharedTargetSurvivesViaTheOtherPath()
    {
      using var events = Diamond().GetArrivalDagnumerator();

      var witnessed = Drain(events, (nodeEvent, walk) =>
      {
        if (nodeEvent.Value == "left")
          walk.SuppressDeparture(0);
      });

      var venture = witnessed.Single(nodeEvent => nodeEvent.Value == "venture");
      CollectionAssert.AreEqual(new[] { ("right", 2, 0.30m) }, venture.Arrivals.Select(Arr).ToList());
    }

    [TestMethod]
    public void SeverAllArrivals_VoidsTheDepartures_ButCannotRetractTheEvent()
    {
      using var events = Chain().GetArrivalDagnumerator();

      var witnessed = Drain(events, (nodeEvent, walk) =>
      {
        if (nodeEvent.Value == "b")
          walk.SeverArrival(0);
      });

      // b's event stood -- verdicts shape only the future -- but its departure died, so c
      // never events.
      CollectionAssert.AreEqual(new[] { "a", "b" }, witnessed.Select(nodeEvent => nodeEvent.Value).ToList());
    }

    [TestMethod]
    public void SeverOneOfTwoArrivals_DoesNotVoidTheDepartures()
    {
      using var events = DiamondWithTail().GetArrivalDagnumerator();

      var witnessed = Drain(events, (nodeEvent, walk) =>
      {
        if (nodeEvent.Value == "venture")
          walk.SeverArrival(0);
      });

      var holdco = witnessed.Single(nodeEvent => nodeEvent.Value == "holdco");
      CollectionAssert.AreEqual(new[] { ("venture", 3, 1.00m) }, holdco.Arrivals.Select(Arr).ToList());
      Assert.IsTrue(holdco.IsSink);
    }

    [TestMethod]
    public void WrappedSources_Compose_WithOrdinalGapsPreserved()
    {
      // The grouping layer sits over ANY forward source. Pruning right removes ordinal 2;
      // the surviving events keep their source ordinals, gaps and all, and the venture's
      // arrival group shows only the live path.
      using var events = Diamond().PruneBefore(node => node == "right").GetArrivalDagnumerator();
      var witnessed = Drain(events);

      CollectionAssert.AreEqual(new[] { 0, 1, 3 }, witnessed.Select(nodeEvent => nodeEvent.Ordinal).ToList());
      CollectionAssert.AreEqual(
        new[] { ("left", 1, 0.70m) },
        witnessed.Single(nodeEvent => nodeEvent.Value == "venture").Arrivals.Select(Arr).ToList());
    }

    [TestMethod]
    public void Verdicts_AreStrict()
    {
      using var events = Diamond().GetArrivalDagnumerator();

      // No event is under dialogue before the first advance.
      Assert.ThrowsException<InvalidOperationException>(() => events.SeverArrival(0));
      Assert.ThrowsException<InvalidOperationException>(() => events.SuppressDeparture(0));

      Assert.IsTrue(events.MoveNext()); // apex: a source with two departures.
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => events.SeverArrival(0));
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => events.SuppressDeparture(2));

      while (events.MoveNext())
      {
      }

      // Nor after exhaustion.
      Assert.IsNull(events.Current);
      Assert.ThrowsException<InvalidOperationException>(() => events.SuppressDeparture(0));
    }
  }
}
