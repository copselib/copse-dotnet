using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The monad laws for the pointed bind, dag-side, on string payloads with concatenation as
  // the composer -- associative and NON-commutative, so composition ORDER is pinned, and with
  // the empty string as a genuine identity the phantom trick can ride. Left identity: bind
  // over a singleton is the selector. Right identity: bind of Return is the source. And
  // associativity on the corpus against the FUSED selector, computed by the operator itself
  // over the first expansion with its slot as a PHANTOM NODE (the tree family's proof device):
  // the phantom is Return'd through the second bind, so it promotes with its siblings,
  // inherits position, and composes payloads exactly as an attached child would -- reading
  // its final in-edges back gives the fused slot. Selectors chain promotions through sharing,
  // starve fragments, hang the slot from two nodes (sharing a tree cannot say), and put
  // payloads on attachments.
  [TestClass]
  public class DagSelectManyLawTests
  {
    private const string Phantom = "⟂";
    // The OUTSIDE, as a node: a phantom source standing for the original's in-edge parents, so
    // "slot as source" is an edge the bound result can show (the builder rightly refuses to
    // call a reached node a source, which is exactly the case of an attached-and-as-source slot).
    private const string Outside = "⊤";

    private static string Concat(string upstream, string downstream) => upstream + downstream;

    private static DagExpansion<string, string> Return(string value) => DagExpansion<string, string>.Return(value);
    private static DagExpansion<string, string> Leaf(string value) => DagExpansion<string, string>.Leaf(value);
    private static DagExpansion<string, string> Drop => DagExpansion<string, string>.Drop;
    private static DagExpansion<string, string> Promote => DagExpansion<string, string>.Promote;

    // apex -a-> left -c-> venture, apex -b-> right -d-> venture; plus a tail venture -e-> tail.
    private static Dag<string, string> Diamond()
    {
      var apex = new DagNode<string, string>("apex");
      var left = apex.AddChild("left", "a");
      var right = apex.AddChild("right", "b");
      var venture = new DagNode<string, string>("venture");
      left.AddChild(venture, "c");
      right.AddChild(venture, "d");
      venture.AddChild("tail", "e");
      return new Dag<string, string>(apex);
    }

    private static Dag<string, string> SharedLeaf()
    {
      var alpha = new DagNode<string, string>("alpha");
      var beta = new DagNode<string, string>("beta");
      var middle = alpha.AddChild("middle", "m");
      var leaf = new DagNode<string, string>("leaf");
      alpha.AddChild(leaf, "x");
      beta.AddChild(leaf, "y");
      middle.AddChild(leaf, "z");
      return new Dag<string, string>(alpha, beta);
    }

    private static Dag<string, string> Chain()
    {
      var a = new DagNode<string, string>("a");
      a.AddChild("b", "p").AddChild("c", "q");
      return new Dag<string, string>(a);
    }

    private static IEnumerable<(string Name, Func<Dag<string, string>> Factory)> Corpus()
    {
      yield return ("diamond", Diamond);
      yield return ("sharedLeaf", SharedLeaf);
      yield return ("chain", Chain);
    }

    // Selectors by name, so failures say which pair broke.
    private static IEnumerable<(string Name, Func<string, DagExpansion<string, string>> Selector)> Selectors()
    {
      yield return ("return", node => Return(node));
      yield return ("promoteMiddles", node => node == "left" || node == "right" || node == "middle" || node == "b" ? Promote : Return(node));
      yield return ("promoteShared", node => node == "venture" || node == "leaf" ? Promote : Return(node));
      yield return ("dropShared", node => node == "venture" || node == "leaf" || node == "c" ? Drop : Return(node));
      yield return ("leafMiddles", node => node == "left" || node == "middle" || node == "b" ? Leaf(node) : Return(node));
      yield return ("splitSlotLast", node => node == "left" || node == "alpha" || node == "b"
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2" }, new[] { (0, 1, "i") }, DagSlot<string>.Under(1))
        : Return(node));
      yield return ("splitSlotFirstVia", node => node == "right" || node == "middle" || node == "a"
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2" }, new[] { (0, 1, "j") }, DagSlot<string>.Under((0, "k")))
        : Return(node));
      yield return ("outsideVia", node => node == "left" || node == "alpha" || node == "b"
        ? DagExpansion<string, string>.Of(new[] { node + "?" }, new (int, int, string)[0], DagSlot<string>.Of(DagSlotAttachment<string>.FromOutside("o"), DagSlotAttachment<string>.Under(0, "u")))
        : Return(node));
      yield return ("forkSharedSlot", node => node == "apex" || node == "beta" || node == "b"
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2", node + "3" }, new[] { (0, 1, "f"), (0, 2, "g") }, DagSlot<string>.Under(1, 2))
        : Return(node));
      yield return ("sourceAndSlot", node => node == "venture" || node == "leaf" || node == "c"
        ? DagExpansion<string, string>.Of(new[] { node + "!" }, new (int, int, string)[0], DagSlot<string>.Of(DagSlotAttachment<string>.Under(0), DagSlotAttachment<string>.FromOutside()))
        : Return(node));
    }

    [TestMethod]
    public void LeftIdentity_BindOverASingleton_IsTheSelector()
    {
      foreach (var (name, selector) in Selectors())
      {
        var singleton = new Dag<string, string>(new DagNode<string, string>("only"));
        var expansion = selector("only");
        Assert.AreEqual(
          DagWalkerCorpusStrings.Content(FragmentAsDag(expansion, withPhantom: false)),
          DagWalkerCorpusStrings.Content(singleton.SelectMany(selector, Concat)),
          name);
      }
    }

    [TestMethod]
    public void RightIdentity_BindOfReturn_IsTheSource()
    {
      foreach (var (name, factory) in Corpus())
        Assert.AreEqual(DagWalkerCorpusStrings.Content(factory()), DagWalkerCorpusStrings.Content(factory().SelectMany(node => Return(node), Concat)), name);
    }

    [TestMethod]
    public void Associativity_StepwiseIsFused_OverTheCorpus()
    {
      var pairs = 0;
      foreach (var (dagName, factory) in Corpus())
        foreach (var (fName, f) in Selectors())
          foreach (var (gName, g) in Selectors())
          {
            var stepwise = factory().SelectMany(f, Concat).SelectMany(g, Concat);
            var fused = factory().SelectMany(node => Fuse(f(node), g), Concat);
            Assert.AreEqual(
              DagWalkerCorpusStrings.Content(stepwise),
              DagWalkerCorpusStrings.Content(fused),
              $"{dagName}: {fName} then {gName}");
            pairs++;
          }
      Assert.AreEqual(3 * 10 * 10, pairs);
    }

    [TestMethod]
    public void ThePhantomReadsBackTheQuartet()
    {
      // Sanity for the proof device: fusing each quartet member with Return is itself.
      Assert.AreEqual("[x] under 0", Describe(Fuse(Return("x"), node => Return(node))));
      Assert.AreEqual("[x] none", Describe(Fuse(Leaf("x"), node => Return(node))));
      Assert.AreEqual("[] none", Describe(Fuse(Drop, node => Return(node))));
      Assert.AreEqual("[] outside", Describe(Fuse(Promote, node => Return(node))));
      // And the interesting ones: a promoted holder hands the slot up, composing the payload.
      Assert.AreEqual("[x1] under 0 via ik", Describe(Fuse(
        DagExpansion<string, string>.Of(new[] { "x1", "x2" }, new[] { (0, 1, "i") }, DagSlot<string>.Under((1, "k"))),
        node => node == "x2" ? Promote : Return(node))));
      Assert.AreEqual("[] outside", Describe(Fuse(Return("x"), node => Promote)));
      Assert.AreEqual("[x] none", Describe(Fuse(Return("x"), node => Leaf(node))));
    }

    // The Kleisli composite via the phantom: the first expansion as a dag with its slot as a
    // node, bound by g with the phantom Return'd, read back.
    private static DagExpansion<string, string> Fuse(DagExpansion<string, string> first, Func<string, DagExpansion<string, string>> g)
    {
      if (first.IsEmpty && !first.HasSlot)
        return Drop;

      var bound = FragmentAsDag(first, withPhantom: true).SelectMany(node => node == Phantom || node == Outside ? Return(node) : g(node), Concat);

      var values = bound.GetTopologicalOrder().Where(value => value != Phantom && value != Outside).ToList();
      var indexOf = values.Select((value, index) => (value, index)).ToDictionary(pair => pair.value, pair => pair.index);
      var edges = bound.GetEdges().ToList();
      var internalEdges = edges.Where(edge => edge.Parent != Phantom && edge.Child != Phantom && edge.Parent != Outside).Select(edge => (indexOf[edge.Parent], indexOf[edge.Child], edge.Edge)).ToArray();
      var attachments = edges.Where(edge => edge.Child == Phantom)
        .Select(edge => edge.Parent == Outside
          ? (edge.Edge.Length == 0 ? DagSlotAttachment<string>.FromOutside() : DagSlotAttachment<string>.FromOutside(edge.Edge))
          : (edge.Edge.Length == 0 ? DagSlotAttachment<string>.Under(indexOf[edge.Parent]) : DagSlotAttachment<string>.Under(indexOf[edge.Parent], edge.Edge)))
        .ToArray();
      var slot = DagSlot<string>.Of(attachments);

      if (values.Count == 0 && slot.IsNone)
        return Drop;

      return DagExpansion<string, string>.Of(values.ToArray(), internalEdges, slot);
    }

    private static Dag<string, string> FragmentAsDag(DagExpansion<string, string> expansion, bool withPhantom)
    {
      var nodes = expansion.Values.Select(value => new DagNode<string, string>(value)).ToArray();
      var hasInternalIn = new bool[nodes.Length];
      foreach (var (from, to, edge) in expansion.Edges)
      {
        nodes[from].AddChild(nodes[to], edge);
        hasInternalIn[to] = true;
      }

      var sources = nodes.Where((node, index) => !hasInternalIn[index]).ToList();

      if (!withPhantom)
        return new Dag<string, string>(sources);

      // The outside feeds every fragment source (the original's in-edges, as one phantom parent),
      // and the phantom slot hangs wherever the slot says -- inside or from the outside.
      var outside = new DagNode<string, string>(Outside);
      foreach (var source in sources)
        outside.AddChild(source, "");

      if (expansion.HasSlot)
      {
        var phantom = new DagNode<string, string>(Phantom);
        foreach (var attachment in expansion.Slot.Attachments)
          (attachment.IsFromOutside ? outside : nodes[attachment.FragmentNode]).AddChild(phantom, attachment.HasPayload ? attachment.Payload : "");
      }

      return new Dag<string, string>(outside);
    }

    private static string Describe(DagExpansion<string, string> expansion)
    {
      var slot = expansion.Slot.IsNone ? "none" : string.Join(",", expansion.Slot.Attachments.Select(attachment => attachment.ToString()));
      return $"[{string.Join(",", expansion.Values)}] {slot}";
    }
  }

  // The content reading for string-payload dags (DagWalkerCorpus.Content is decimal-typed).
  internal static class DagWalkerCorpusStrings
  {
    public static string Content(IDagnumerable<string, string> dag)
    {
      var nodes = dag.GetTopologicalOrder().OrderBy(node => node, StringComparer.Ordinal);
      var edges = dag.GetEdges().Select(edge => $"{edge.Parent}->{edge.Child}:{edge.Edge}").OrderBy(text => text, StringComparer.Ordinal);
      var sources = dag.GetSources().OrderBy(node => node, StringComparer.Ordinal);
      return $"nodes[{string.Join(",", nodes)}] edges[{string.Join(",", edges)}] sources[{string.Join(",", sources)}]";
    }
  }
}
