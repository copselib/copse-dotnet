using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace Copse.Linq.Tests
{
  // Arbitrary text as node values: the value-token layer (CSV-style minimal quoting, '""' for a
  // literal quote, one escape set shared by both grammars) makes EVERY string a legal node
  // value in both layouts and both tiers. Values without special characters serialize
  // byte-identical to the unquoted format, wrong-layout detection fires only on UNQUOTED
  // structure, and unquoted line endings are insignificant everywhere (they were already
  // skipped by three of the four readers; now all four agree).
  [TestClass]
  public class SerializerQuotingTests
  {
    // Every kind of hostile value: each structural character of both grammars, quotes (single,
    // doubled, interior), backslashes (NOT special -- no inflation), line endings, empty,
    // whitespace-only, surrounding whitespace, and multi-byte text.
    private static readonly string[] NastyValues =
    {
      "(", ")", ",", ";", "|",
      "\"", "\"\"", "a\"b", "He said \"hi\"",
      "a,b", "a(b)", "x;y|z", "(a,b);(c|d)",
      "C:\\temp\\trees.txt", "\\", "\\\\",
      "line1\nline2", "line1\r\nline2", "\n",
      "", " ", "\t", "  leading", "trailing  ", "inner space",
      "café🌳",
    };

    private static readonly string[] Shapes = { "a", "a(b(c))", "a(b,c)", "a,b,c", "a(b(d,e),c)" };

    // The same shape with each node's value swapped for a nasty one; cycling the offset over
    // the corpus puts every nasty value in every structural position.
    private static ITreenumerable<string> NastyTree(string shape, int offset)
      => EngineTree.Parse(shape).Select(context => NastyValues[(context.Node[0] - 'a' + offset) % NastyValues.Length]);

    private static string[] ScheduledValues(ITreenumerator<string> treenumerator)
    {
      var values = new List<string>();

      using (treenumerator)
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            values.Add(treenumerator.Node);

      return values.ToArray();
    }

    // ---------------------------------------------------------------------------------------
    // Round trips: serialize a tree of hostile values, read it back, lockstep-compare the
    // full visit stream (values AND positions) against the original. All four tiers.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void StringDepthFirst_RoundTripsArbitraryText()
    {
      foreach (var shape in Shapes)
        for (var offset = 0; offset < NastyValues.Length; offset++)
        {
          var expected = NastyTree(shape, offset);
          var actual = TreeSerializer.DeserializeDepthFirstTree(expected.SerializeDepthFirstTree());

          VisitStreamConformance.AssertSameStream(
            expected.GetDepthFirstTreenumerator(),
            actual.GetDepthFirstTreenumerator(),
            VisitStreamConformance.TraverseAll,
            $"dft-string nasty {shape} +{offset}");
        }
    }

    [TestMethod]
    public void StringBreadthFirst_RoundTripsArbitraryText()
    {
      foreach (var shape in Shapes)
        for (var offset = 0; offset < NastyValues.Length; offset++)
        {
          var expected = NastyTree(shape, offset);
          var actual = TreeSerializer.DeserializeBreadthFirstTree(expected.SerializeBreadthFirstTree());

          VisitStreamConformance.AssertSameStream(
            expected.GetBreadthFirstTreenumerator(),
            actual.GetBreadthFirstTreenumerator(),
            VisitStreamConformance.TraverseAll,
            $"bft-string nasty {shape} +{offset}");
        }
    }

    [TestMethod]
    public void StreamDepthFirst_RoundTripsArbitraryText()
    {
      foreach (var shape in Shapes)
        for (var offset = 0; offset < NastyValues.Length; offset++)
        {
          var expected = NastyTree(shape, offset);
          var payload = expected.SerializeDepthFirstTree();
          var actual = TreeSerializer.DeserializeDepthFirstTree(() => new StringReader(payload));

          VisitStreamConformance.AssertSameStream(
            expected.GetDepthFirstTreenumerator(),
            actual.GetDepthFirstTreenumerator(),
            VisitStreamConformance.TraverseAll,
            $"dft-stream nasty {shape} +{offset}");
        }
    }

    [TestMethod]
    public void StreamBreadthFirst_RoundTripsArbitraryText()
    {
      foreach (var shape in Shapes)
        for (var offset = 0; offset < NastyValues.Length; offset++)
        {
          var expected = NastyTree(shape, offset);
          var payload = expected.SerializeBreadthFirstTree();
          var actual = TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader(payload));

          VisitStreamConformance.AssertSameStream(
            expected.GetBreadthFirstTreenumerator(),
            actual.GetBreadthFirstTreenumerator(),
            VisitStreamConformance.TraverseAll,
            $"bft-stream nasty {shape} +{offset}");
        }
    }

    // ---------------------------------------------------------------------------------------
    // Payload shapes: quoting is MINIMAL (clean values are byte-identical to the unquoted
    // format) and the escape set is the union of both grammars' structural characters.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void QuotedPayloadShapes()
    {
      string Dft(string value) => EngineTree.Parse("a").Select(context => value).SerializeDepthFirstTree();

      Assert.AreEqual("x", Dft("x"), "clean values stay bare");
      Assert.AreEqual("a b", Dft("a b"), "interior whitespace stays bare");
      Assert.AreEqual(@"C:\temp\x", Dft(@"C:\temp\x"), "backslashes are not special");

      Assert.AreEqual("\"a,b\"", Dft("a,b"));
      Assert.AreEqual("\"x;y\"", Dft("x;y"), "the escape set is the union of BOTH grammars");
      Assert.AreEqual("\"\"", Dft(""), "the empty value is representable");
      Assert.AreEqual("\"\"\"\"", Dft("\""), "a value that IS one quote: open, doubled literal, close");
      Assert.AreEqual("\"He said \"\"hi\"\"\"", Dft("He said \"hi\""));
      Assert.AreEqual("\" x \"", Dft(" x "), "surrounding whitespace forces quotes");
      Assert.AreEqual("\"line1\nline2\"", Dft("line1\nline2"));

      Assert.AreEqual(
        "\"a,b\"(c)",
        EngineTree.Parse("a(b)").Select(context => context.Node == "a" ? "a,b" : "c").SerializeDepthFirstTree());

      Assert.AreEqual(
        "\"a(x)\";c",
        EngineTree.Parse("a(b)").Select(context => context.Node == "a" ? "a(x)" : "c").SerializeBreadthFirstTree());
    }

    // ---------------------------------------------------------------------------------------
    // Parsing rules: quoted structure is data; unquoted line endings are insignificant in all
    // four readers; bare tokens are lenient about interior quotes.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void QuotedStructuralCharactersAreDataNotStructure()
    {
      // A quoted ';' does not trip the depth-first reader's wrong-layout detection...
      CollectionAssert.AreEqual(
        new[] { "a;b", "c" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("\"a;b\"(c)").GetDepthFirstTreenumerator()));

      // ...and quoted parens do not trip the breadth-first reader's.
      CollectionAssert.AreEqual(
        new[] { "a(x)", "b" },
        ScheduledValues(TreeSerializer.DeserializeBreadthFirstTree("\"a(x)\";b").GetBreadthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a;b", "c" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("\"a;b\"(c)")).GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a(x)", "b" },
        ScheduledValues(TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader("\"a(x)\";b")).GetBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public void UnquotedLineEndingsAreInsignificantInAllFourReaders()
    {
      // Line-wrapped payloads parse as if unwrapped...
      CollectionAssert.AreEqual(
        new[] { "a", "b", "c" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("a(\r\nb,\r\nc)").GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a", "b", "c" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("a(\r\nb,\r\nc)")).GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a", "b", "c" },
        ScheduledValues(TreeSerializer.DeserializeBreadthFirstTree("a;\r\nb,c").GetBreadthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a", "b", "c" },
        ScheduledValues(TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader("a;\r\nb,c")).GetBreadthFirstTreenumerator()));

      // ...and a line ending interrupting a bare token contributes nothing (both string tiers
      // historically disagreed here; the value-token layer unifies them).
      CollectionAssert.AreEqual(
        new[] { "a", "bc" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("a(b\nc)").GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a", "bc" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("a(b\nc)")).GetDepthFirstTreenumerator()));

      // Quoted line endings are value characters.
      CollectionAssert.AreEqual(
        new[] { "b\nc" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("\"b\nc\"").GetDepthFirstTreenumerator()));
    }

    [TestMethod]
    public void BareTokenWithInteriorQuoteIsLiteral()
    {
      // A quote can only OPEN a quoted token as the token's first character; anywhere else in a
      // bare token it is a literal (hand-authored payloads stay forgiving -- the writer always
      // quotes values containing quotes).
      CollectionAssert.AreEqual(
        new[] { "a", "b\"c" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("a(b\"c)").GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a", "b\"c" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("a(b\"c)")).GetDepthFirstTreenumerator()));
    }

    [TestMethod]
    public void EmptyValuesAreRepresentable()
    {
      CollectionAssert.AreEqual(
        new[] { "" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("\"\"").GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "a", "", "b" },
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("a(\"\",b)").GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        new[] { "", "" },
        ScheduledValues(TreeSerializer.DeserializeBreadthFirstTree("\"\";\"\"").GetBreadthFirstTreenumerator()));
    }

    // ---------------------------------------------------------------------------------------
    // Malformed quoting fails fast.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void UnterminatedQuoteThrows()
    {
      Assert.ThrowsException<FormatException>(() =>
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("\"abc").GetDepthFirstTreenumerator()));

      Assert.ThrowsException<FormatException>(() =>
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("\"abc")).GetDepthFirstTreenumerator()));
    }

    [TestMethod]
    public void TextAfterClosingQuoteThrows()
    {
      Assert.ThrowsException<FormatException>(() =>
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree("\"ab\"cd").GetDepthFirstTreenumerator()));

      Assert.ThrowsException<FormatException>(() =>
        ScheduledValues(TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("\"ab\"cd")).GetDepthFirstTreenumerator()));
    }

    // ---------------------------------------------------------------------------------------
    // Quoting does not disturb laziness: composing parses nothing, and a small pulled prefix
    // maps a small prefix of a deeply quoted payload.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void QuotedParsingStaysLazy()
    {
      var payload = "\"a,a\"(\"b,b\"(\"c,c\"(\"d,d\"(\"e,e\"(\"f,f\")))))";
      var mapCalls = 0;

      var tree = TreeSerializer.DeserializeDepthFirstTree(payload, value => { mapCalls++; return value; });

      Assert.AreEqual(0, mapCalls, "composition must parse nothing");

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
      }

      Assert.IsTrue(mapCalls <= 3, $"expected a small parsed prefix, but the map ran {mapCalls} times");
    }
  }
}
