using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // THE LAW BATTERY (design-docs/SUBSTITUTION_TAXONOMY.md): the monad laws for
  // ReplaceNodes over value-dependent selectors -- the gate on the reserved SelectMany name.
  // Return is Keep; bind is ReplaceNodes; the fused selector x => bind(f(x), g) is computed by
  // running ReplaceNodes over f(x) as a standalone dag (the operator verifying itself).
  //
  // Associativity is asserted on CONTENT (nodes + edges, canonicalized): the readiness clause
  // makes total cross-node order a presentation fact, and provenance (SourceOrdinal) is
  // pass-relative by design, so neither participates in the laws. The two tree-side
  // counterexample SHAPES (single-leaf chain + interior drop; branching + leaf drop) are
  // regression fixtures here precisely because they killed the every-leaf placement -- the
  // every-node wiring must pass both, and the locality thesis says why it does: every
  // replacement node holds its own edge to each neighbor, so a second-pass Drop removes
  // exactly its own copies.
  [TestClass]
  public class DagNodeSubstitutionLawTests
  {
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

    private static Dag<string, decimal> Edge()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 5m);
      return new Dag<string, decimal>(a);
    }

    private static Dag<string, decimal> Singleton(string value) =>
      new(new DagNode<string, decimal>(value));

    // ---- content extraction and comparison (presentation-independent; distinct values
    // ---- throughout the fixtures make the value its own identity) ----

    private static (string[] Values, string[] Edges) Content(IDagnumerable<string, decimal> dag)
    {
      var values = new List<string>();
      var denseByStream = new Dictionary<int, int>();
      var rawEdges = new List<(int ParentStream, int ChildStream, decimal Payload)>();

      using var walk = dag.GetDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.EnteringNode)
        {
          denseByStream[walk.Ordinal] = values.Count;
          values.Add(walk.Node);
        }
        else if (walk.ParentOrdinal >= 0)
        {
          rawEdges.Add((walk.ParentOrdinal, walk.Ordinal, walk.Edge));
        }
      }

      var edges = rawEdges
        .Select(edge => $"{values[denseByStream[edge.ParentStream]]}->{values[denseByStream[edge.ChildStream]]}:{edge.Payload}")
        .OrderBy(edge => edge, StringComparer.Ordinal)
        .ToArray();

      return (values.OrderBy(value => value, StringComparer.Ordinal).ToArray(), edges);
    }

    private static void AssertSameContent(IDagnumerable<string, decimal> expected, IDagnumerable<string, decimal> actual)
    {
      var expectedContent = Content(expected);
      var actualContent = Content(actual);

      CollectionAssert.AreEqual(expectedContent.Values, actualContent.Values);
      CollectionAssert.AreEqual(expectedContent.Edges, actualContent.Edges);
    }

    // ---- test-side fragment specs (public surface only): null = Drop ----

    private sealed class Frag
    {
      public Frag(string[] values, params (int From, int To, decimal Edge)[] edges)
      {
        Values = values;
        Edges = edges;
      }

      public string[] Values { get; }
      public (int From, int To, decimal Edge)[] Edges { get; }

      public DagNodeGraph<string, decimal> ToGraph() =>
        DagNodeGraph<string, decimal>.Graph(Values, Edges);

      public Dag<string, decimal> ToDag()
      {
        var nodes = Values.Select(value => new DagNode<string, decimal>(value)).ToArray();
        var hasInternalIn = new bool[Values.Length];

        foreach (var (from, to, edge) in Edges)
        {
          nodes[from].AddChild(nodes[to], edge);
          hasInternalIn[to] = true;
        }

        return new Dag<string, decimal>(nodes.Where((_, index) => !hasInternalIn[index]));
      }
    }

    private static Func<string, DagNodeGraph<string, decimal>> ToSelector(Func<string, Frag> spec) =>
      node => spec(node)?.ToGraph() ?? DagNodeGraph<string, decimal>.Drop;

    private static Frag KeepFrag(string value) => new(new[] { value });

    // The fused selector h(x) = bind(f(x), g), computed by the operator itself over the
    // fragment as a standalone dag -- Drop when f drops or when g starves the fragment.
    private static Func<string, DagNodeGraph<string, decimal>> Fused(
      Func<string, Frag> f,
      Func<string, Frag> g)
    {
      return node =>
      {
        var fragment = f(node);

        if (fragment == null)
          return DagNodeGraph<string, decimal>.Drop;

        var bound = fragment.ToDag().ReplaceNodes(ToSelector(g));

        if (bound.Count == 0)
          return DagNodeGraph<string, decimal>.Drop;

        var values = new List<string>();
        var denseByStream = new Dictionary<int, int>();
        var rawEdges = new List<(int ParentStream, int ChildStream, decimal Payload)>();

        using var walk = bound.GetDagnumerator();
        while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        {
          if (walk.Mode == DagnumeratorMode.EnteringNode)
          {
            denseByStream[walk.Ordinal] = values.Count;
            values.Add(walk.Node);
          }
          else if (walk.ParentOrdinal >= 0)
          {
            rawEdges.Add((walk.ParentOrdinal, walk.Ordinal, walk.Edge));
          }
        }

        // Entries arrive in topological order, so mapped edges run forward -- Graph's
        // From < To constraint holds by construction.
        var edges = rawEdges
          .Select(edge => (denseByStream[edge.ParentStream], denseByStream[edge.ChildStream], edge.Payload))
          .ToArray();

        return DagNodeGraph<string, decimal>.Graph(values.ToArray(), edges);
      };
    }

    private static void AssertAssociative(Dag<string, decimal> dag, Func<string, Frag> f, Func<string, Frag> g)
    {
      var twoStep = dag.ReplaceNodes(ToSelector(f)).ReplaceNodes(ToSelector(g));
      var fused = dag.ReplaceNodes(Fused(f, g));

      AssertSameContent(twoStep, fused);
    }

    // ---- the laws ----

    [TestMethod]
    public void LeftIdentity_BindOverASingletonIsTheSelector()
    {
      var spec = new Frag(new[] { "p", "q", "r" }, (0, 1, 1m), (0, 2, 2m));

      var bound = Singleton("a").ReplaceNodes(node => spec.ToGraph());

      AssertSameContent(spec.ToDag(), bound);
    }

    [TestMethod]
    public void LeftIdentity_DropOverASingletonIsEmpty()
    {
      Assert.AreEqual(0, Singleton("a").ReplaceNodes(node => DagNodeGraph<string, decimal>.Drop).Count);
    }

    [TestMethod]
    public void RightIdentity_KeepReconstructsTheDag()
    {
      var kept = Diamond().ReplaceNodes(DagNodeGraph<string, decimal>.Keep);

      AssertSameContent(Diamond(), kept);

      // Stronger than content: Keep occupies the original seats, so presentation and
      // provenance survive too.
      CollectionAssert.AreEqual(new[] { "apex", "left", "right", "venture" }, kept.GetTopologicalOrder().ToArray());
      for (var ordinal = 0; ordinal < kept.Count; ordinal++)
        Assert.AreEqual(ordinal, kept.SourceOrdinal(ordinal));
    }

    [TestMethod]
    public void Associativity_TheSingleLeafChainDropShape()
    {
      // Tree counterexample 1's shape: a chain expansion, then the interior tip dropped.
      // Every-leaf placement with deletion FAILED here (b died in one association and
      // survived in the other); every-node wiring passes because a.p holds its own edge
      // to b, so dropping a.q removes exactly a.q's copies.
      AssertAssociative(
        Edge(),
        node => node == "a" ? new Frag(new[] { "a.p", "a.q" }, (0, 1, 1m)) : KeepFrag(node),
        node => node == "a.q" ? null : KeepFrag(node));
    }

    [TestMethod]
    public void Associativity_TheBranchingLeafDropShape()
    {
      // Tree counterexample 2's shape: a branching expansion, then one branch dropped.
      // Every-leaf placement with promotion FAILED here (a rescued copy the fused side
      // never makes); every-node wiring passes for the same locality reason.
      AssertAssociative(
        Edge(),
        node => node == "a" ? new Frag(new[] { "a.p", "a.q", "a.r" }, (0, 1, 1m), (0, 2, 2m)) : KeepFrag(node),
        node => node == "a.q" ? null : KeepFrag(node));
    }

    [TestMethod]
    public void Associativity_SplitThenChainAcrossSharing()
    {
      // The two lawful fragments mixed over shared structure -- the composition that
      // escapes both tree-side fragments in one fusion step.
      AssertAssociative(
        Diamond(),
        node => node == "left" ? new Frag(new[] { "l.0", "l.1" }) : KeepFrag(node),
        node => node == "l.0" ? new Frag(new[] { "l.0.head", "l.0.tail" }, (0, 1, 1m)) : KeepFrag(node));
    }

    [TestMethod]
    public void Associativity_ASecondPassDropThatStarvesAWholeFragment()
    {
      // g drops every node of f's replacement: the fused selector must come out Drop, and
      // liveness must agree across the associations (venture survives via right).
      AssertAssociative(
        Diamond(),
        node => node == "left" ? new Frag(new[] { "l.head", "l.tail" }, (0, 1, 1m)) : KeepFrag(node),
        node => node.StartsWith("l.") ? null : KeepFrag(node));
    }

    [TestMethod]
    public void Associativity_FirstPassDropComposesWithSecondPassExpansion()
    {
      // f drops a shared parent (liveness spares the venture through right); g then divides
      // the survivor's parent -- drops and expansions interleaved across passes.
      AssertAssociative(
        Diamond(),
        node => node == "left" ? null : KeepFrag(node),
        node => node == "right" ? new Frag(new[] { "r.0", "r.1" }) : KeepFrag(node));
    }

    [TestMethod]
    public void Associativity_EverythingAtOnce()
    {
      // The stress mix: a split, a chain, a drop, and seats kept, over shared structure.
      AssertAssociative(
        Diamond(),
        node => node switch
        {
          "apex" => new Frag(new[] { "x.0", "x.1" }),
          "left" => new Frag(new[] { "l.head", "l.tail" }, (0, 1, 1m)),
          _ => KeepFrag(node),
        },
        node => node switch
        {
          "l.tail" => null,
          "x.1" => new Frag(new[] { "x.1.a", "x.1.b" }, (0, 1, 3m)),
          _ => KeepFrag(node),
        });
    }
  }
}
