using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The subdivision battery: the bijection as an operator pair, and the EDGE GRAIN derived --
  // SelectEdges, PruneEdges, and ReplaceEdges are the one bind restricted to edge elements of
  // the subdivision, content-exact over the corpus. Round trips both ways; the shape pinned by
  // hand; and the parity predicate refusing, with coordinates, exactly what the family refuses
  // by principle: a promoted edge element is edge contraction.
  [TestClass]
  public class DagSubdivisionTests
  {
    private static DagExpansion<DagElement<string, decimal>, Unit> Return(DagElement<string, decimal> element)
      => DagExpansion<DagElement<string, decimal>, Unit>.Return(element);

    private static IEnumerable<(string Name, Func<Dag<string, decimal>> Factory)> Corpus()
      => DagWalkerCorpus.All().Concat(new[] { ("parallel", (Func<Dag<string, decimal>>)ParallelEdges) });

    private static Dag<string, decimal> ParallelEdges()
    {
      var top = new DagNode<string, decimal>("top");
      var mid = top.AddChild("mid", 0.25m);
      top.AddChild(mid, 0.75m);
      var bottom = mid.AddChild("bottom", 0.5m);
      top.AddChild(bottom, 0.1m);
      return new Dag<string, decimal>(top);
    }

    [TestMethod]
    public void TheShape_PinnedByHand()
    {
      var subdivided = DagWalkerCorpus.Diamond().Subdivide();
      CollectionAssert.AreEqual(
        new[] { "apex", "[apex -0.60-> left]", "[apex -0.40-> right]", "left", "[left -0.70-> venture]", "right", "[right -0.30-> venture]", "venture" },
        subdivided.GetTopologicalOrder().Select(element => element.ToString()).ToList());
      Assert.AreEqual(8, subdivided.Count);
      Assert.AreEqual(8, subdivided.GetEdges().Count(), "one edge in, one out per edge element");
      Assert.AreEqual(1, subdivided.GetTopologicalOrder().Single(element => element.IsEdge && element.Edge.Parent == "right").Edge.InEdgeIndex, "the venture's second arrival");
      Assert.AreEqual(0, subdivided.SourceOrdinal(0), "node elements keep their seats");
      Assert.AreEqual(-1, subdivided.SourceOrdinal(1), "edge elements are born here");
    }

    [TestMethod]
    public void RoundTrip_SubdivideThenUnsubdivide_IsTheSource_SeatsIncluded()
    {
      foreach (var (name, factory) in Corpus())
      {
        var source = factory().Materialize();
        var roundTripped = source.Subdivide().Unsubdivide();
        Assert.AreEqual(DagWalkerCorpus.Content(source), DagWalkerCorpus.Content(roundTripped), name);
        for (var ordinal = 0; ordinal < source.Count; ordinal++)
          Assert.AreEqual(source.SourceOrdinal(ordinal), roundTripped.SourceOrdinal(ordinal), $"seat {ordinal} [{name}]");
      }
    }

    [TestMethod]
    public void RoundTrip_UnsubdivideThenSubdivide_IsTheSubdivision()
    {
      foreach (var (name, factory) in Corpus())
      {
        var subdivided = factory().Subdivide();
        var again = subdivided.Unsubdivide().Subdivide();
        CollectionAssert.AreEqual(
          subdivided.GetTopologicalOrder().Select(element => element.ToString()).ToList(),
          again.GetTopologicalOrder().Select(element => element.ToString()).ToList(),
          name);
      }
    }

    [TestMethod]
    public void SelectEdges_IsBindOfReturn_OnEdgeElements()
    {
      foreach (var (name, factory) in Corpus())
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().SelectEdges(context => context.Edge * 2)),
          DagWalkerCorpus.Content(factory().Subdivide()
            .SelectMany(element => element.IsEdge
              ? Return(DagElement<string, decimal>.OfEdge(new DagEdgeContext<string, decimal>(element.Edge.Parent, element.Edge.Child, element.Edge.Edge * 2, element.Edge.InEdgeIndex)))
              : Return(element), Unit.Compose)
            .Unsubdivide()),
          name);
    }

    private static IEnumerable<Func<DagEdgeContext<string, decimal>, bool>> EdgePredicates()
    {
      yield return context => context.Edge == 0.70m || context.Edge == 0.3m || context.Edge == 0.75m;
      yield return context => context.Parent == "apex" || context.Parent == "alpha" || context.Parent == "top" || context.Parent == "a";
      yield return context => context.Child == "venture" || context.Child == "sharedLeaf" || context.Child == "bottom";
      yield return context => context.InEdgeIndex == 1;
      yield return context => true;
      yield return context => false;
    }

    [TestMethod]
    public void PruneEdges_IsBindOfDropOrReturn_OnEdgeElements()
    {
      foreach (var (name, factory) in Corpus())
        foreach (var predicate in EdgePredicates())
          Assert.AreEqual(
            DagWalkerCorpus.Content(factory().PruneEdges(predicate)),
            DagWalkerCorpus.Content(factory().Subdivide()
              .SelectMany(element => element.IsEdge && predicate(element.Edge) ? DagExpansion<DagElement<string, decimal>, Unit>.Drop : Return(element), Unit.Compose)
              .Unsubdivide()),
            name);
    }

    private static IEnumerable<(string Name, Func<DagEdgeContext<string, decimal>, DagEdgePath<string, decimal>> Selector)> PathSelectors()
    {
      yield return ("keepDoubled", context => DagEdgePath<string, decimal>.Keep(context.Edge * 2));
      yield return ("throughAnchors", context => context.Child == "venture" || context.Child == "sharedLeaf" || context.Child == "bottom" || context.Child == "b"
        ? DagEdgePath<string, decimal>.Through(1m, $"anchor:{context.Parent}>{context.Child}#{context.InEdgeIndex}", context.Edge)
        : DagEdgePath<string, decimal>.Keep(context.Edge));
      yield return ("chainsAndDrops", context => context.Parent == "apex" || context.Parent == "alpha" || context.Parent == "top"
        ? DagEdgePath<string, decimal>.Chain(0.5m, new DagEdgePathLink<string, decimal>($"hop1:{context.Child}#{context.InEdgeIndex}", 0.2m), new DagEdgePathLink<string, decimal>($"hop2:{context.Child}#{context.InEdgeIndex}", context.Edge))
        : context.Edge == 0.70m ? DagEdgePath<string, decimal>.Drop : DagEdgePath<string, decimal>.Keep(context.Edge));
    }

    [TestMethod]
    public void ReplaceEdges_IsBindOfThePath_OnEdgeElements()
    {
      foreach (var (name, factory) in Corpus())
        foreach (var (selectorName, selector) in PathSelectors())
          Assert.AreEqual(
            DagWalkerCorpus.Content(factory().ReplaceEdges(selector)),
            DagWalkerCorpus.Content(factory().Subdivide()
              .SelectMany(element => element.IsEdge ? PathAsExpansion(selector(element.Edge), element.Edge) : Return(element), Unit.Compose)
              .Unsubdivide()),
            $"{name}/{selectorName}");
    }

    // A path is a chain-shaped expansion of the edge element -- edge, node, edge, node, ... ,
    // edge -- with the slot under the last edge element; Drop is Drop.
    private static DagExpansion<DagElement<string, decimal>, Unit> PathAsExpansion(DagEdgePath<string, decimal> path, DagEdgeContext<string, decimal> context)
    {
      if (path.IsDrop)
        return DagExpansion<DagElement<string, decimal>, Unit>.Drop;

      var values = new List<DagElement<string, decimal>> { DagElement<string, decimal>.OfEdge(new DagEdgeContext<string, decimal>(context.Parent, context.Child, path.FirstEdge, context.InEdgeIndex)) };
      foreach (var link in path.Links)
      {
        values.Add(DagElement<string, decimal>.OfNode(link.Node));
        values.Add(DagElement<string, decimal>.OfEdge(new DagEdgeContext<string, decimal>(link.Node, context.Child, link.Edge, 0)));
      }

      var edges = Enumerable.Range(0, values.Count - 1).Select(index => (index, index + 1, Unit.Value)).ToArray();

      return DagExpansion<DagElement<string, decimal>, Unit>.Of(values.ToArray(), edges, DagSlot<Unit>.Under(values.Count - 1));
    }

    [TestMethod]
    public void TheParityPredicate_RefusesContractionAndDangling_WithCoordinates()
    {
      var subdivided = DagWalkerCorpus.Diamond().Subdivide();

      var contracted = subdivided.SelectMany(element => element.IsEdge && element.Edge.Parent == "left" ? DagExpansion<DagElement<string, decimal>, Unit>.Promote : Return(element), Unit.Compose);
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => contracted.Unsubdivide()).Message, "CONTRACTION");

      var dangling = subdivided.SelectMany(element => element.IsEdge && element.Edge.Parent == "left" ? DagExpansion<DagElement<string, decimal>, Unit>.Leaf(element) : Return(element), Unit.Compose);
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => dangling.Unsubdivide()).Message, "dangling");

      var adjacentEdges = subdivided.SelectMany(element => !element.IsEdge && element.Node == "left" ? DagExpansion<DagElement<string, decimal>, Unit>.Promote : Return(element), Unit.Compose);
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => adjacentEdges.Unsubdivide()).Message, "parity must alternate");
    }
  }
}
