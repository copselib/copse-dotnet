using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // ToFormattedLines/ToFormattedString pins: the diamond with its shared node expanded once
  // and referenced with #ordinal + the return glyph, parallel edges as two distinct branch
  // lines, multi-source islands stacked at the root column, edge-text omission for edge-less
  // payloads, and composition over wrapped sources.
  [TestClass]
  public class DagFormattingTests
  {
    private static string InvariantEdge(decimal edge) => edge.ToString(CultureInfo.InvariantCulture);

    [TestMethod]
    public void Diamond_SharedNodeExpandsOnce_ThenReferences()
    {
      var lines = DagWalkerCorpus.Diamond().ToFormattedLines(node => node, InvariantEdge);

      CollectionAssert.AreEqual(
        new[]
        {
          "apex",
          "├─ 0.60 → left",
          "│  └─ 0.70 → venture #3",
          "└─ 0.40 → right",
          "   └─ 0.30 → venture #3 ↺",
        },
        lines.ToList());
    }

    [TestMethod]
    public void Chain_DefaultFormatters()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m).AddChild("c", 1m);
      var lines = new Dag<string, decimal>(a).ToFormattedLines();

      CollectionAssert.AreEqual(
        new[]
        {
          "a",
          "└─ 1 → b",
          "   └─ 1 → c",
        },
        lines.ToList());
    }

    [TestMethod]
    public void ParallelEdges_EachRendersItsOwnBranchLine()
    {
      var top = new DagNode<string, decimal>("top");
      var bottom = new DagNode<string, decimal>("bottom");
      top.AddChild(bottom, 0.25m);
      top.AddChild(bottom, 0.75m);
      var lines = new Dag<string, decimal>(top).ToFormattedLines(node => node, InvariantEdge);

      CollectionAssert.AreEqual(
        new[]
        {
          "top",
          "├─ 0.25 → bottom #1",
          "└─ 0.75 → bottom #1 ↺",
        },
        lines.ToList());
    }

    [TestMethod]
    public void MultiSource_IslandsStackAtTheRootColumn()
    {
      var island1 = new DagNode<string, decimal>("island1");
      island1.AddChild("island1Child", 1m);
      var island2 = new DagNode<string, decimal>("island2");
      var lines = new Dag<string, decimal>(island1, island2).ToFormattedLines(node => node, InvariantEdge);

      CollectionAssert.AreEqual(
        new[]
        {
          "island1",
          "└─ 1 → island1Child",
          "island2",
        },
        lines.ToList());
    }

    [TestMethod]
    public void EmptyEdgeText_OmitsThePayloadSegment()
    {
      var a = new DagNode<string, string>("a");
      a.AddChild("b", null);
      var lines = new Dag<string, string>(a).ToFormattedLines();

      CollectionAssert.AreEqual(new[] { "a", "└─ b" }, lines.ToList());
    }

    [TestMethod]
    public void WrappedSources_RenderTheLiveStructure()
    {
      // Pruning right leaves the single live path -- and venture's live in-degree is now 1,
      // so its #ordinal tag disappears with the sharing.
      var lines = DagWalkerCorpus.Diamond().PruneNodesBefore(node => node == "right").ToFormattedLines(node => node, InvariantEdge);

      CollectionAssert.AreEqual(
        new[]
        {
          "apex",
          "└─ 0.60 → left",
          "   └─ 0.70 → venture",
        },
        lines.ToList());
    }

    [TestMethod]
    public void ToFormattedString_JoinsWithNewlines()
    {
      var rendered = DagWalkerCorpus.Diamond().ToFormattedString(node => node, InvariantEdge);

      Assert.AreEqual(5, rendered.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length);
      StringAssert.StartsWith(rendered, "apex");
    }
  }
}
