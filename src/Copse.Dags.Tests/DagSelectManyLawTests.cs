using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The monad laws for the pointed bind, dag-side, on string payloads with concatenation as
  // the composer -- associative and NON-commutative, so composition ORDER is pinned, and with
  // the empty string as a genuine identity the phantom device can ride. Left identity: bind
  // over a singleton is the selector. Right identity: bind of Return is the source. And
  // associativity on the corpus against the FUSED selector, computed by the operator itself
  // over the first expansion with its slot as PHANTOM NODES (the tree family's proof device,
  // grown the nodes a dag needs): the OUTSIDE ⊤ feeds the fragment's sources, so an attachment
  // from outside is an edge the reading can see; and every original out-edge is a phantom
  // child ⊥i hanging from each attachment with the payload the attachment answers, so a
  // second bind's answers at the holders are exercised and read back -- one attachment per
  // surviving ⊥-edge, answering exactly its index. Selectors chain promotions through
  // sharing, starve fragments, hang the slot from two nodes (sharing a tree cannot say), put
  // payloads on attachments, and answer for departures: rewrite, suppress, from inside and
  // from outside.
  [TestClass]
  public class DagSelectManyLawTests
  {
    private const string Phantom = "⟂";
    private const string Outside = "⊤";
    private const string Departure = "⊥";

    private static string Concat(string upstream, string downstream) => upstream + downstream;

    private static DagExpansion<string, string> Return(string value) => DagExpansion<string, string>.Return(value);
    private static DagExpansion<string, string> Leaf(string value) => DagExpansion<string, string>.Leaf(value);
    private static DagExpansion<string, string> Drop => DagExpansion<string, string>.Drop;
    private static DagExpansion<string, string> Promote => DagExpansion<string, string>.Promote;
    private static DagExpansion<string, string> Single(string value, DagSlot<string> slot) => DagExpansion<string, string>.Of(new[] { value }, new (int, int, string)[0], slot);

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

    private static bool IsMiddle(string node) => node == "left" || node == "right" || node == "middle" || node == "b";
    private static bool IsShared(string node) => node == "venture" || node == "leaf" || node == "c";
    private static bool IsTop(string node) => node == "apex" || node == "alpha" || node == "beta" || node == "a";

    // Selectors by name, so failures say which pair broke.
    private static IEnumerable<(string Name, Func<string, DagExpansion<string, string>> Selector)> Selectors()
    {
      yield return ("return", node => Return(node));
      yield return ("promoteMiddles", node => IsMiddle(node) ? Promote : Return(node));
      yield return ("promoteShared", node => IsShared(node) ? Promote : Return(node));
      yield return ("dropShared", node => IsShared(node) ? Drop : Return(node));
      yield return ("leafMiddles", node => IsMiddle(node) ? Leaf(node) : Return(node));
      yield return ("splitSlotLast", node => node == "left" || node == "alpha" || node == "b"
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2" }, new[] { (0, 1, "i") }, DagSlot<string>.Under(1))
        : Return(node));
      yield return ("splitSlotFirstVia", node => node == "right" || node == "middle" || node == "a"
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2" }, new[] { (0, 1, "j") }, DagSlot<string>.Under((0, "k")))
        : Return(node));
      yield return ("outsideVia", node => node == "left" || node == "alpha" || node == "b"
        ? Single(node + "?", DagSlot<string>.Of(DagSlotAttachment<string>.FromOutside("o"), DagSlotAttachment<string>.Under(0, "u")))
        : Return(node));
      yield return ("forkSharedSlot", node => node == "apex" || node == "beta" || node == "b"
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2", node + "3" }, new[] { (0, 1, "f"), (0, 2, "g") }, DagSlot<string>.Under(1, 2))
        : Return(node));
      yield return ("sourceAndSlot", node => IsShared(node)
        ? Single(node + "!", DagSlot<string>.Of(DagSlotAttachment<string>.Under(0), DagSlotAttachment<string>.FromOutside()))
        : Return(node));
      // The answers.
      yield return ("rewriteDepartures", node => IsTop(node) || IsMiddle(node)
        ? Single(node, DagSlot<string>.Under(0).Answering((index, payload) => DagDepartureAnswer<string>.Rewrite(payload + "r" + index)))
        : Return(node));
      yield return ("suppressFirstDeparture", node => IsTop(node) || IsShared(node)
        ? Single(node, DagSlot<string>.Under(0).Answering((index, payload) => index == 0 ? DagDepartureAnswer<string>.Suppress : DagDepartureAnswer<string>.Keep))
        : Return(node));
      yield return ("promoteAnswering", node => IsMiddle(node)
        ? DagExpansion<string, string>.Of(new string[0], new (int, int, string)[0], DagSlot<string>.Of(DagSlotAttachment<string>.FromOutside("o").Answering((index, payload) => DagDepartureAnswer<string>.Rewrite("w" + payload))))
        : Return(node));
      yield return ("splitAnsweringSuppressOdd", node => IsTop(node)
        ? DagExpansion<string, string>.Of(new[] { node + "1", node + "2" }, new[] { (0, 1, "s") }, DagSlot<string>.Under((1, "v")).Answering((index, payload) => index % 2 == 1 ? DagDepartureAnswer<string>.Suppress : DagDepartureAnswer<string>.Rewrite(payload + "!")))
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
          DagWalkerCorpusStrings.Content(FragmentAsDag(expansion, withPhantoms: false, departures: Array.Empty<string>())),
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
      // The lawful fragment: every pair except an earlier PROMOTION followed by a later
      // ANSWER at a holder -- the non-law pinned below. Answering then promoting, answering
      // then answering, and a promotion answering its own departures all associate.
      var pairs = 0;
      var selectors = Selectors().ToList();
      var lawfulPairs = 0;

      foreach (var (dagName, factory) in Corpus())
      {
        var departuresOf = factory().GetEdges().GroupBy(edge => edge.Parent).ToDictionary(group => group.Key, group => group.Select(edge => edge.Edge).ToArray());

        foreach (var (fName, f) in selectors)
          foreach (var (gName, g) in selectors)
          {
            pairs++;

            if (Promoting.Contains(fName) && HolderAnswering.Contains(gName))
              continue;

            lawfulPairs++;
            var stepwise = factory().SelectMany(f, Concat).SelectMany(g, Concat);
            var fused = factory().SelectMany(node => Fuse(f(node), g, departuresOf.TryGetValue(node, out var departures) ? departures : Array.Empty<string>()), Concat);
            Assert.AreEqual(
              DagWalkerCorpusStrings.Content(stepwise),
              DagWalkerCorpusStrings.Content(fused),
              $"{dagName}: {fName} then {gName}");
          }
      }

      Assert.AreEqual(3 * selectors.Count * selectors.Count, pairs);
      Assert.AreEqual(3 * (selectors.Count * selectors.Count - Promoting.Count * HolderAnswering.Count), lawfulPairs, "every pair outside promotion-then-answering is exercised");
    }

    // Selectors with an attachment from outside anywhere (promotion composes a suffix onto the
    // edges above), and selectors answering by index or payload at a holder.
    private static readonly HashSet<string> Promoting = new HashSet<string> { "promoteMiddles", "promoteShared", "outsideVia", "sourceAndSlot", "promoteAnswering" };
    private static readonly HashSet<string> HolderAnswering = new HashSet<string> { "rewriteDepartures", "suppressFirstDeparture", "splitAnsweringSuppressOdd" };

    [TestMethod]
    public void PromotionThenAnswering_IsAPrincipledNonLaw_TheSuffixLandsBehindTheAnswer()
    {
      // Stepwise, promoting left first composes apex's edge to "ac"; the second bind's rewrite at
      // the apex then sees "ac" and appends "r0". Fused, the apex's answer can only see its own
      // payload "a" -- the suffix "c" belongs to left's expansion, not the apex's -- and the bind
      // composes it afterward: "ar0c". No local answer reproduces "acr0": it would need the
      // grandchild edge, which locality forbids. So a per-edge answer is lawful exactly when no
      // earlier pass promoted beneath it -- the promotion-free fragment, the dag's reading of
      // the sequence lab's suffix-free fragment. Payload- and index-blind answers (keep all,
      // suppress all) are the quartet and always lawful.
      Func<string, DagExpansion<string, string>> promoteMiddles = node => IsMiddle(node) ? Promote : Return(node);
      Func<string, DagExpansion<string, string>> rewriteDepartures = node => IsTop(node) || IsMiddle(node)
        ? Single(node, DagSlot<string>.Under(0).Answering((index, payload) => DagDepartureAnswer<string>.Rewrite(payload + "r" + index)))
        : Return(node);
      var departuresOf = Diamond().GetEdges().GroupBy(edge => edge.Parent).ToDictionary(group => group.Key, group => group.Select(edge => edge.Edge).ToArray());

      var stepwise = Diamond().SelectMany(promoteMiddles, Concat).SelectMany(rewriteDepartures, Concat);
      var fused = Diamond().SelectMany(node => Fuse(promoteMiddles(node), rewriteDepartures, departuresOf.TryGetValue(node, out var departures) ? departures : Array.Empty<string>()), Concat);

      StringAssert.Contains(DagWalkerCorpusStrings.Content(stepwise), "apex->venture:acr0");
      StringAssert.Contains(DagWalkerCorpusStrings.Content(fused), "apex->venture:ar0c");
    }

    [TestMethod]
    public void ThePhantomReadsBackTheQuartet()
    {
      // Sanity for the proof device, with one departure "d" so the slot is observable: fusing
      // each quartet member with Return is itself, answers folded into the read-back.
      var one = new[] { "d" };
      Assert.AreEqual("[x] under 0 → d", Describe(Fuse(Return("x"), node => Return(node), one)));
      Assert.AreEqual("[x] none", Describe(Fuse(Leaf("x"), node => Return(node), one)));
      Assert.AreEqual("[] none", Describe(Fuse(Drop, node => Return(node), one)));
      Assert.AreEqual("[] outside → d", Describe(Fuse(Promote, node => Return(node), one)));
      // A promoted holder hands the slot up, composing the payload in front of the departure.
      Assert.AreEqual("[x1] under 0 → ikd", Describe(Fuse(
        DagExpansion<string, string>.Of(new[] { "x1", "x2" }, new[] { (0, 1, "i") }, DagSlot<string>.Under((1, "k"))),
        node => node == "x2" ? Promote : Return(node), one)));
      Assert.AreEqual("[] outside → d", Describe(Fuse(Return("x"), node => Promote, one)));
      Assert.AreEqual("[x] none", Describe(Fuse(Return("x"), node => Leaf(node), one)));
      // A second bind answering at the holder: rewrite composes after the first's answer.
      Assert.AreEqual("[x] under 0 → dr0R", Describe(Fuse(
        Single("x", DagSlot<string>.Under(0).Answering((index, payload) => DagDepartureAnswer<string>.Rewrite(payload + "r" + index))),
        node => Single(node, DagSlot<string>.Under(0).Answering((index, payload) => DagDepartureAnswer<string>.Rewrite(payload + "R"))), one)));
      Assert.AreEqual("[x] none", Describe(Fuse(
        Return("x"),
        node => Single(node, DagSlot<string>.Under(0).Answering((index, payload) => DagDepartureAnswer<string>.Suppress)), one)));
    }

    // The Kleisli composite via the phantoms: the first expansion as a dag -- its slot's
    // departures as phantom children, the outside as a phantom source -- bound by g with every
    // phantom Return'd, read back.
    private static DagExpansion<string, string> Fuse(DagExpansion<string, string> first, Func<string, DagExpansion<string, string>> g, string[] departures)
    {
      if (first.IsEmpty && !first.HasSlot)
        return Drop;

      var bound = FragmentAsDag(first, withPhantoms: true, departures)
        .SelectMany(node => node == Outside || node.StartsWith(Departure, StringComparison.Ordinal) ? Return(node) : g(node), Concat);

      var values = bound.GetTopologicalOrder().Where(value => value != Outside && !value.StartsWith(Departure, StringComparison.Ordinal)).ToList();
      var indexOf = values.Select((value, index) => (value, index)).ToDictionary(pair => pair.value, pair => pair.index);
      var edges = bound.GetEdges().ToList();
      var internalEdges = edges
        .Where(edge => indexOf.ContainsKey(edge.Parent) && indexOf.ContainsKey(edge.Child))
        .Select(edge => (indexOf[edge.Parent], indexOf[edge.Child], edge.Edge))
        .ToArray();

      // One attachment per surviving ⊥-edge, answering exactly its index with the payload the
      // device composed (attachment payloads folded in); every other index suppressed.
      var attachments = edges
        .Where(edge => edge.Child.StartsWith(Departure, StringComparison.Ordinal))
        .Select(edge =>
        {
          var departureIndex = int.Parse(edge.Child.Substring(Departure.Length));
          var answered = edge.Edge;
          Func<int, string, DagDepartureAnswer<string>> answer = (index, payload) => index == departureIndex ? DagDepartureAnswer<string>.Rewrite(answered) : DagDepartureAnswer<string>.Suppress;
          return edge.Parent == Outside
            ? DagSlotAttachment<string>.FromOutside().Answering(answer)
            : DagSlotAttachment<string>.Under(indexOf[edge.Parent]).Answering(answer);
        })
        .ToArray();

      var slot = DagSlot<string>.Of(attachments);

      if (values.Count == 0 && slot.IsNone)
        return Drop;

      return DagExpansion<string, string>.Of(values.ToArray(), internalEdges, slot);
    }

    private static Dag<string, string> FragmentAsDag(DagExpansion<string, string> expansion, bool withPhantoms, string[] departures)
    {
      var nodes = expansion.Values.Select(value => new DagNode<string, string>(value)).ToArray();
      var hasInternalIn = new bool[nodes.Length];
      foreach (var (from, to, edge) in expansion.Edges)
      {
        nodes[from].AddChild(nodes[to], edge);
        hasInternalIn[to] = true;
      }

      var sources = nodes.Where((node, index) => !hasInternalIn[index]).ToList();

      if (!withPhantoms)
        return new Dag<string, string>(sources);

      // The outside feeds every fragment source (the original's in-edges, as one phantom parent);
      // every attachment hangs the departures it answers for, as phantom children.
      var outside = new DagNode<string, string>(Outside);
      foreach (var source in sources)
        outside.AddChild(source, "");

      var phantomDepartures = departures.Select((payload, index) => new DagNode<string, string>(Departure + index)).ToArray();

      foreach (var attachment in expansion.Slot.Attachments)
      {
        var holder = attachment.IsFromOutside ? outside : nodes[attachment.FragmentNode];

        for (var index = 0; index < departures.Length; index++)
        {
          var answer = attachment.Answer(index, departures[index]);

          if (answer.IsSuppress)
            continue;

          var answered = answer.IsRewrite ? answer.Payload : departures[index];
          holder.AddChild(phantomDepartures[index], attachment.HasPayload ? attachment.Payload + answered : answered);
        }
      }

      return new Dag<string, string>(outside);
    }

    private static string Describe(DagExpansion<string, string> expansion)
    {
      var slot = expansion.Slot.IsNone
        ? "none"
        : string.Join(",", expansion.Slot.Attachments.Select(attachment =>
        {
          var origin = attachment.IsFromOutside ? "outside" : $"under {attachment.FragmentNode}";
          var answer = attachment.Answer(0, "d");
          return answer.IsSuppress ? $"{origin} → ∅" : $"{origin} → {(answer.IsRewrite ? answer.Payload : "d")}";
        }));
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
