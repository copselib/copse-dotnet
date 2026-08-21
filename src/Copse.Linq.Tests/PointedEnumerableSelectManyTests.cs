using Copse.Linq.Experimental;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The sequence LAB for the pointed bind (SELECTMANY_DESIGN.md Addenda II-III): the same
  // {0, 1}-slot algebra over the simplest carrier, where the slot is visibly THE
  // CONTINUATION. One canonical form (the tagged stream), two factory families (a-priori
  // and discovered placement), one driver (the stack of paused enumerators). Grounded
  // against LINQ's own operators, then the monad laws with Kleisli composition implemented
  // AS the operator; then what only laziness can show; then the bracket discipline and the
  // operator's contract that an expansion is never pulled ahead of its emission.
  [TestClass]
  public class PointedEnumerableSelectManyTests
  {
    private static readonly int[][] Sources =
    {
      new int[0],
      new[] { 1 },
      new[] { 1, 2 },
      new[] { 1, 2, 3, 4, 5 },
      new[] { 3, 1, 5 },
      new[] { 3, 3, 2, 1 },
      new[] { 1, 4, 3 },
    };

    private static Func<TSource, SequenceExpansion<TResult>> Compose<TSource, TMiddle, TResult>(
      Func<TSource, SequenceExpansion<TMiddle>> first,
      Func<TMiddle, SequenceExpansion<TResult>> second)
      => Experimental.EnumerableExtensions.Compose(first, second);

    private static List<T> Stripped<T>(SequenceExpansion<T> expansion)
      => expansion.Items.Where(item => !item.IsSlot).Select(item => item.Value).ToList();

    // ------------------------------------------------- groundings against LINQ's operators

    [TestMethod]
    public void Grounding_ReturnComposed_IsSelect()
    {
      Func<int, string> map = value => $"v{value}";

      foreach (var source in Sources)
        CollectionAssert.AreEqual(source.Select(map).ToList(), source.SelectMany(value => SequenceExpansion.Return(map(value))).ToList());
    }

    [TestMethod]
    public void Grounding_ReturnOrPromote_IsWhere()
    {
      Func<int, bool> keep = value => value % 2 == 1;

      foreach (var source in Sources)
        CollectionAssert.AreEqual(
          source.Where(keep).ToList(),
          source.SelectMany(value => keep(value) ? SequenceExpansion.Return(value) : SequenceExpansion.Promote<int>()).ToList());
    }

    [TestMethod]
    public void Grounding_ReturnOrDrop_IsTakeWhile()
    {
      Func<int, bool> keep = value => value < 3;

      foreach (var source in Sources)
        CollectionAssert.AreEqual(
          source.TakeWhile(keep).ToList(),
          source.SelectMany(value => keep(value) ? SequenceExpansion.Return(value) : SequenceExpansion.Drop<int>()).ToList());
    }

    [TestMethod]
    public void Grounding_ReturnOrLeaf_IsTakeUntilInclusive()
    {
      Func<int, bool> stopAt = value => value == 3;

      foreach (var source in Sources)
      {
        var expected = source.TakeWhile(value => !stopAt(value)).Concat(source.SkipWhile(value => !stopAt(value)).Take(1)).ToList();

        CollectionAssert.AreEqual(expected, source.SelectMany(value => stopAt(value) ? SequenceExpansion.Leaf(value) : SequenceExpansion.Return(value)).ToList());
      }
    }

    [TestMethod]
    public void Grounding_SlotAtEnd_IsClassicSelectMany()
    {
      Func<int, IEnumerable<string>> classic = value => Enumerable.Range(0, value % 3).Select(index => $"{value}.{index}");

      foreach (var source in Sources)
      {
        CollectionAssert.AreEqual(source.SelectMany(classic).ToList(), source.SelectMany(value => SequenceExpansion.SlotAtEnd(classic(value))).ToList(), "SlotAtEnd");
        CollectionAssert.AreEqual(source.SelectMany(classic).ToList(), source.SelectMany(value => SequenceExpansion.Slotted(classic(value), Enumerable.Empty<string>())).ToList(), "Slotted(items, empty)");
      }
    }

    // --------------------------------------------------------- discovered placement

    [TestMethod]
    public void SlotAfter_PlacesTheSlotAfterTheFirstMatch_AndNotAgain()
    {
      // Element 1's expansion: a, b, c with the slot after the first "b"-ish item -- the
      // continuation (element 2's expansion) lands there; the later match is ignored.
      var result = new[] { 1, 2 }.SelectMany(value => value == 1
        ? SequenceExpansion.SlotAfter(new[] { "a", "b", "c", "b" }, item => item == "b", IfNoMatch.Slotless)
        : SequenceExpansion.Leaf("X")).ToList();

      CollectionAssert.AreEqual(new[] { "a", "b", "X", "c", "b" }, result);
    }

    [TestMethod]
    public void SlotAfter_IndexedForm_SpellsAnAPrioriPositionWithoutSplitting()
    {
      var result = new[] { 1, 2 }.SelectMany(value => value == 1
        ? SequenceExpansion.SlotAfter(new[] { "a", "b", "c" }, (_, index) => index == 1, IfNoMatch.Slotless)
        : SequenceExpansion.Leaf("X")).ToList();

      CollectionAssert.AreEqual(new[] { "a", "b", "X", "c" }, result);
    }

    [TestMethod]
    public void SlotAfter_IfNoMatch_DecidesBetweenStoppingAndSlotAtEnd()
    {
      Func<IfNoMatch, List<string>> run = policy => new[] { 1, 2 }.SelectMany(value => value == 1
        ? SequenceExpansion.SlotAfter(new[] { "a", "b" }, item => item == "never", policy)
        : SequenceExpansion.Leaf("X")).ToList();

      CollectionAssert.AreEqual(new[] { "a", "b" }, run(IfNoMatch.Slotless), "never fired: the stream stops");
      CollectionAssert.AreEqual(new[] { "a", "b", "X" }, run(IfNoMatch.SlotAtEnd), "never fired: the slot lands after the last item");
    }

    // ------------------------------------------------------------------- the monad laws

    // The SUFFIX-FREE selectors -- the sequence carrier's lawful fragment (the finding
    // below): f promotes 2s, drops 4s, leafs 5s, multi-emits 3s, single-emits the rest; g
    // triggers on GENERATED values so composition reaches inside expansions.
    private static SequenceExpansion<string> F(int value)
    {
      if (value % 10 == 2) return SequenceExpansion.Promote<string>();
      if (value % 10 == 4) return SequenceExpansion.Drop<string>();
      if (value % 10 == 5) return SequenceExpansion.Leaf($"{value}a");
      if (value % 10 == 3) return SequenceExpansion.SlotAtEnd(new[] { $"{value}a", $"{value}b" });
      return SequenceExpansion.Return($"{value}a");
    }

    private static SequenceExpansion<string> G(string value)
    {
      if (value.EndsWith("b")) return SequenceExpansion.Drop<string>();
      if (value == "3a") return SequenceExpansion.Leaf(value + "!");
      if (value == "1a") return SequenceExpansion.Promote<string>();
      return SequenceExpansion.SlotAtEnd(new[] { value + "L", value + "M" });
    }

    [TestMethod]
    public void MonadLaw_LeftIdentity()
    {
      foreach (var value in new[] { 1, 2, 3, 4, 5 })
        CollectionAssert.AreEqual(Stripped(F(value)), new[] { value }.SelectMany(F).ToList(), $"bind(Return({value}), f) ≡ f({value}) with its slot consumed");
    }

    [TestMethod]
    public void MonadLaw_RightIdentity()
    {
      foreach (var source in Sources)
        CollectionAssert.AreEqual(source.ToList(), source.SelectMany(SequenceExpansion.Return).ToList());
    }

    [TestMethod]
    public void MonadLaw_Associativity_TheSuffixFreeFragment()
    {
      foreach (var source in Sources)
      {
        var leftAssociated = source.SelectMany(F).SelectMany(G).ToList();
        var rightAssociated = source.SelectMany(Compose<int, string, string>(F, G)).ToList();

        CollectionAssert.AreEqual(leftAssociated, rightAssociated, $"suffix-free associativity [{string.Join(",", source)}]");
      }
    }

    [TestMethod]
    public void MonadLaw_Associativity_DiscoveredPlacementIsSuffixFreeToo()
    {
      // SlotAfter with SlotAtEnd policy never produces an after-slot item, so it lives in
      // the lawful fragment as well.
      Func<int, SequenceExpansion<string>> f = value => SequenceExpansion.SlotAfter(
        Enumerable.Range(0, value % 4).Select(index => $"{value}.{index}"),
        item => item.EndsWith(".1"),
        IfNoMatch.SlotAtEnd);

      foreach (var source in Sources)
        CollectionAssert.AreEqual(
          source.SelectMany(f).SelectMany(G).ToList(),
          source.SelectMany(Compose<int, string, string>(f, G)).ToList(),
          $"discovered-placement associativity [{string.Join(",", source)}]");
    }

    // THE LAB'S FINDING: AFTER-SLOT items break associativity on the flat carrier -- pinned
    // as a documented counterexample, the sequence dual of the tree suite's forest-
    // attachment finding.
    //
    // Mechanism: an after-slot item means "a SIBLING following the continuation," and
    // flattening destroys sibling-versus-descendant. Left-associated, the intermediate
    // [y2, P] is flat, so the second bind reads P as y2's CONTINUATION and y2's suffix
    // nests around P's expansion: y2L, PL, PR, y2R. But any Kleisli composite emits
    // per-element blocks -- composite(1)'s items around composite(2)'s -- so the right side
    // is y2L, y2R, PL, PR, and NO composite can interleave one element's derived material
    // inside another's block. Structural. The tree carrier escapes it because its
    // intermediate REMEMBERS that suffix items are siblings; the sequence's lawful
    // territory is the SUFFIX-FREE fragment (prefix + optional trailing continuation).
    [TestMethod]
    public void Finding_AfterSlotItems_BreakAssociativity_TheCounterexample()
    {
      Func<int, SequenceExpansion<string>> suffixCarrying = value =>
        value == 1 ? SequenceExpansion.Slotted(new string[0], new[] { "P" }) : SequenceExpansion.Return($"y{value}");

      Func<string, SequenceExpansion<string>> expander = value => SequenceExpansion.Slotted(new[] { value + "L" }, new[] { value + "R" });

      var source = new[] { 1, 2 };
      var leftAssociated = source.SelectMany(suffixCarrying).SelectMany(expander).ToList();
      var rightAssociated = source.SelectMany(Compose<int, string, string>(suffixCarrying, expander)).ToList();

      CollectionAssert.AreEqual(new[] { "y2L", "PL", "PR", "y2R" }, leftAssociated, "flat intermediate: P reads as y2's continuation");
      CollectionAssert.AreEqual(new[] { "y2L", "y2R", "PL", "PR" }, rightAssociated, "composite: per-element blocks");
      CollectionAssert.AreNotEqual(leftAssociated, rightAssociated, "the after-slot fragment is non-associative on sequences");
    }

    // ------------------------------------------------------------- the mechanics, pinned

    [TestMethod]
    public void Suffixes_NestInReverseOrder()
    {
      var result = new[] { 1, 2 }.SelectMany(value => SequenceExpansion.Slotted(new[] { $"{value}pre" }, new[] { $"{value}post" })).ToList();

      CollectionAssert.AreEqual(new[] { "1pre", "2pre", "2post", "1post" }, result);
    }

    [TestMethod]
    public void Streaming_AnInfiniteSourceStopsAtTheFirstSlotlessExpansion()
    {
      CollectionAssert.AreEqual(
        new[] { 0, 1, 2, 3 },
        InfiniteCount().SelectMany(value => value < 4 ? SequenceExpansion.Return(value) : SequenceExpansion.Drop<int>()).ToList());
    }

    [TestMethod]
    public void Streaming_TheSourceIsNotPulledPastTheStop()
    {
      var pulled = new List<int>();

      IEnumerable<int> Instrumented()
      {
        for (var value = 0; ; value++)
        {
          pulled.Add(value);
          yield return value;
        }
      }

      Instrumented().SelectMany(value => value == 2 ? SequenceExpansion.Drop<int>() : SequenceExpansion.Return(value)).ToList();

      CollectionAssert.AreEqual(new[] { 0, 1, 2 }, pulled, "the stop abandons the source, it does not drain it");
    }

    [TestMethod]
    public void Laziness_AnInfinitePrefixStreams()
    {
      CollectionAssert.AreEqual(
        new[] { 0, 1, 2, 3, 4 },
        new[] { 1 }.SelectMany(value => SequenceExpansion.Slotted(InfiniteCount(), Enumerable.Empty<int>())).Take(5).ToList());
    }

    [TestMethod]
    public void Laziness_AnInfiniteSuffixStreams()
    {
      // Element 1 opens a bracket whose suffix never ends; element 2 stops the stream; the
      // bracket then drains forever -- and a finite prefix of it is served lazily.
      var result = new[] { 1, 2 }
        .SelectMany(value => value == 1 ? SequenceExpansion.Slotted(new[] { 100 }, InfiniteCount()) : SequenceExpansion.Leaf(200))
        .Take(5)
        .ToList();

      CollectionAssert.AreEqual(new[] { 100, 200, 0, 1, 2 }, result);
    }

    [TestMethod]
    public void Laziness_ConstructionEnumeratesNothing_BindEnumeratesOnce()
    {
      var enumerations = 0;

      IEnumerable<int> Counted()
      {
        enumerations++;
        yield return 1;
      }

      var slotted = SequenceExpansion.Slotted(Counted(), Counted());
      var discovered = SequenceExpansion.SlotAfter(Counted(), _ => true, IfNoMatch.Slotless);
      Assert.AreEqual(0, enumerations, "no factory enumerates anything");

      new[] { 1 }.SelectMany(value => slotted).ToList();
      Assert.AreEqual(2, enumerations, "Slotted: each sequence exactly once");

      new[] { 1 }.SelectMany(value => discovered).ToList();
      Assert.AreEqual(3, enumerations, "SlotAfter: once");
    }

    [TestMethod]
    public void Laziness_SelectorWorkHappensInStreamOrder()
    {
      var log = new List<string>();

      IEnumerable<string> Logged(string name, params string[] items)
      {
        log.Add($"enumerate {name}");
        foreach (var item in items)
          yield return item;
      }

      var result = new[] { 1, 2 }.SelectMany(value =>
      {
        log.Add($"select {value}");
        return SequenceExpansion.Slotted(Logged($"pre{value}", $"p{value}"), Logged($"post{value}", $"s{value}"));
      }).ToList();

      CollectionAssert.AreEqual(new[] { "p1", "p2", "s2", "s1" }, result);
      CollectionAssert.AreEqual(new[] { "select 1", "enumerate pre1", "select 2", "enumerate pre2", "enumerate post2", "enumerate post1" }, log);
    }

    // The operator's contract: an expansion is never pulled ahead of its emission. Every
    // pull is immediately followed by the emission of that item, with no other pull in
    // between -- including across a discovered slot, where the decision is made on the
    // item already emitted.
    [TestMethod]
    public void Contract_AnExpansionIsNeverPulledAheadOfItsEmission()
    {
      var log = new List<string>();

      IEnumerable<string> Pulled(params string[] items)
      {
        foreach (var item in items)
        {
          log.Add($"pull {item}");
          yield return item;
        }
      }

      var stream = new[] { 1, 2 }.SelectMany(value => value == 1
        ? SequenceExpansion.SlotAfter(Pulled("a", "b", "c"), item => item == "b", IfNoMatch.Slotless)
        : SequenceExpansion.SlotAfter(Pulled("x", "y"), item => item == "x", IfNoMatch.Slotless));

      foreach (var item in stream)
        log.Add($"emit {item}");

      CollectionAssert.AreEqual(
        new[] { "pull a", "emit a", "pull b", "emit b", "pull x", "emit x", "pull y", "emit y", "pull c", "emit c" },
        log);
    }

    // ------------------------------------------------------------ the bracket discipline

    // A hand-rolled enumerable, not an iterator method: iterator bodies (and their finally
    // blocks) run only from the first MoveNext, so they cannot observe a bracket that was
    // acquired and then closed without being started.
    private sealed class TrackedSequence
    {
      public int Acquired;
      public int Disposed;

      public IEnumerable<int> Items(int count) => new Sequence(this, count);

      private sealed class Sequence : IEnumerable<int>
      {
        private readonly TrackedSequence _Owner;
        private readonly int _Count;

        public Sequence(TrackedSequence owner, int count) { _Owner = owner; _Count = count; }

        public IEnumerator<int> GetEnumerator()
        {
          _Owner.Acquired++;
          return new Cursor(_Owner, _Count);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
      }

      private sealed class Cursor : IEnumerator<int>
      {
        private readonly TrackedSequence _Owner;
        private readonly int _Count;
        private int _Next;

        public Cursor(TrackedSequence owner, int count) { _Owner = owner; _Count = count; }

        public int Current { get; private set; }
        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext()
        {
          if (_Next >= _Count)
            return false;

          Current = _Next++;
          return true;
        }

        public void Reset() => _Next = 0;
        public void Dispose() => _Owner.Disposed++;
      }
    }

    [TestMethod]
    public void Brackets_EverySuffixEnumeratorIsDisposed_OnFullDrain()
    {
      var tracked = new TrackedSequence();

      new[] { 1, 2, 3 }.SelectMany(value => SequenceExpansion.Slotted(new[] { value }, tracked.Items(2))).ToList();

      Assert.AreEqual(3, tracked.Acquired);
      Assert.AreEqual(3, tracked.Disposed, "every opened bracket closed");
    }

    // A paused bracket holds a LIVE enumerator when the slot was discovered mid-stream: a
    // SlotAfter expansion pauses inside its items sequence. (A Slotted expansion pauses
    // before its suffix is even acquired -- lazier still -- so it cannot show this.)
    [TestMethod]
    public void Brackets_EveryPausedEnumeratorIsDisposed_OnEarlyTermination()
    {
      var tracked = new TrackedSequence();

      // Each element pauses after its first item with its items enumerator live; the
      // consumer stops after element 3's first item with all three brackets open.
      var third = new[] { 1, 2, 3 }
        .SelectMany(value => SequenceExpansion.SlotAfter(tracked.Items(3), item => item == 0, IfNoMatch.Slotless))
        .Skip(2)
        .First();

      Assert.AreEqual(0, third);
      Assert.AreEqual(3, tracked.Acquired, "three brackets were open at the stop, each holding a live enumerator");
      Assert.AreEqual(3, tracked.Disposed, "and all three closed when the consumer let go");
    }

    [TestMethod]
    public void Brackets_EveryPausedEnumeratorIsDisposed_OnAnExceptionMidStream()
    {
      var tracked = new TrackedSequence();

      var stream = new[] { 1, 2, 3 }.SelectMany(value => value == 3
        ? throw new InvalidOperationException("selector failure")
        : SequenceExpansion.SlotAfter(tracked.Items(3), item => item == 0, IfNoMatch.Slotless));

      Assert.ThrowsException<InvalidOperationException>(() => stream.ToList());
      Assert.AreEqual(2, tracked.Acquired, "two brackets were paused when the third selector threw");
      Assert.AreEqual(2, tracked.Disposed, "both closed on the way out");
    }

    private static IEnumerable<int> InfiniteCount()
    {
      for (var value = 0; ; value++)
        yield return value;
    }
  }
}
