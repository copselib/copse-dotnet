using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Pins the exact box-drawing output of ToFormattedLines/ToFormattedString. The glyph rules
  // (├ interior / └ exterior branches, │/space ancestor columns, padding expansion) are the
  // public rendering contract; the sync/async equality tests in AsyncQueryTests only prove the
  // two colors agree, not that either is right.
  [TestClass]
  public class FormattedLinesTests
  {
    private static readonly (string Tree, string[] ExpectedLines)[] UnpaddedCases =
    {
      ("a", new[] { "a" }),
      ("a,b,c", new[] { "a", "b", "c" }),
      ("a(b,c,d)", new[] { "a", "├b", "├c", "└d" }),
      ("a(b(e),c)", new[] { "a", "├b", "│└e", "└c" }),
      ("a(b(d,e),c)", new[] { "a", "├b", "│├d", "│└e", "└c" }),
      ("a(b(d,e,f),c(g,h,i))", new[] { "a", "├b", "│├d", "│├e", "│└f", "└c", " ├g", " ├h", " └i" }),
      ("a(b(c(d(e))))", new[] { "a", "└b", " └c", "  └d", "   └e" }),
      ("a(b(c),d(e)),f(g)", new[] { "a", "├b", "│└c", "└d", " └e", "f", "└g" }),
      ("a(b,c(d(e,f),g),h)", new[] { "a", "├b", "├c", "│├d", "││├e", "││└f", "│└g", "└h" }),
    };

    private static readonly (string Tree, string[] ExpectedLines)[] PaddedCases =
    {
      ("a(b,c,d)", new[] { "a", "├──b", "├──c", "└──d" }),
      ("a(b(e),c)", new[] { "a", "├──b", "│  └──e", "└──c" }),
      ("a(b(d,e,f),c(g,h,i))", new[] { "a", "├──b", "│  ├──d", "│  ├──e", "│  └──f", "└──c", "   ├──g", "   ├──h", "   └──i" }),
      ("a(b(c(d(e))))", new[] { "a", "└──b", "   └──c", "      └──d", "         └──e" }),
      ("a(b(c),d(e)),f(g)", new[] { "a", "├──b", "│  └──c", "└──d", "   └──e", "f", "└──g" }),
      ("a(b,c(d(e,f),g),h)", new[] { "a", "├──b", "├──c", "│  ├──d", "│  │  ├──e", "│  │  └──f", "│  └──g", "└──h" }),
    };

    [TestMethod]
    public void ToFormattedLines_RendersThePinnedGlyphs()
    {
      foreach (var (tree, expectedLines) in UnpaddedCases)
        CollectionAssert.AreEqual(expectedLines, TreeSerializer.DeserializeDepthFirstTree(tree).ToFormattedLines(0).ToList(), $"padding=0 {tree}");

      foreach (var (tree, expectedLines) in PaddedCases)
        CollectionAssert.AreEqual(expectedLines, TreeSerializer.DeserializeDepthFirstTree(tree).ToFormattedLines(2).ToList(), $"padding=2 {tree}");
    }

    [TestMethod]
    public void ToFormattedString_JoinsTheSameLines()
    {
      foreach (var (tree, expectedLines) in PaddedCases)
        Assert.AreEqual(
          string.Join(Environment.NewLine, expectedLines),
          TreeSerializer.DeserializeDepthFirstTree(tree).ToFormattedString(2),
          $"{tree}");
    }
  }
}
