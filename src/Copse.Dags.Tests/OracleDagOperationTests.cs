using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  [TestClass]
  public class OracleDagOperationTests
  {
    // The canonical diamond:  apex -> left, right;  left, right -> shared.
    private static (Dag<string, int> Dag, DagNode<string, int> Apex, DagNode<string, int> Left, DagNode<string, int> Right, DagNode<string, int> Shared) BuildDiamond()
    {
      var apex = new DagNode<string, int>("apex");
      var left = apex.AddChild("left");
      var right = apex.AddChild("right");
      var shared = new DagNode<string, int>("shared");
      left.AddChild(shared);
      right.AddChild(shared);

      return (new Dag<string, int>(apex), apex, left, right, shared);
    }

    [TestMethod]
    public void Select_MapsValues_AndPreservesSharing()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      var lengths = dag.OracleSelect(node => node.Value.Length);

      var topologicalOrder = lengths.GetTopologicalOrder();
      Assert.AreEqual(4, topologicalOrder.Count); // shared node cloned ONCE, not unfolded to 5

      var apexClone = lengths.Sources[0];
      Assert.AreEqual("apex".Length, apexClone.Value);
      Assert.AreSame(
        apexClone.Children[0].Children[0],
        apexClone.Children[1].Children[0]); // still a shared instance under both parents
    }

    [TestMethod]
    public void Select_RunsOncePerNode_NotOncePerPath()
    {
      var (dag, _, _, _, _) = BuildDiamond();
      var selectorCallCount = 0;

      dag.OracleSelect(node => ++selectorCallCount);

      Assert.AreEqual(4, selectorCallCount);
    }

    [TestMethod]
    public void Select_AggregatesAcrossImmediateChildren()
    {
      var parent = new DagNode<int, int>(0);
      parent.AddChild(3);
      parent.AddChild(4);

      var childSums = new Dag<int, int>(parent).OracleSelect(
        node => node.Children.Sum(child => child.Value));

      Assert.AreEqual(7, childSums.Sources[0].Value);
    }

    [TestMethod]
    public void Select_LeavesTheSourceUntouched()
    {
      var (dag, apex, _, _, _) = BuildDiamond();

      dag.OracleSelect(node => node.Value.ToUpperInvariant());

      Assert.AreEqual("apex", apex.Value);
      Assert.AreEqual(2, apex.Children.Count);
      Assert.AreEqual(4, dag.GetTopologicalOrder().Count);
    }

    [TestMethod]
    public void Select_PreservesParallelEdges()
    {
      var parent = new DagNode<int, int>(1);
      var child = new DagNode<int, int>(2);
      parent.AddChild(child);
      parent.AddChild(child);

      var doubled = new Dag<int, int>(parent).OracleSelect(node => node.Value * 2);
      var parentClone = doubled.Sources[0];

      Assert.AreEqual(2, parentClone.Children.Count);
      Assert.AreSame(parentClone.Children[0], parentClone.Children[1]);
    }

    [TestMethod]
    public void SelectEdges_MapsPayloads_AndCarriesNodeValues()
    {
      var parent = new DagNode<string, decimal>("parent");
      parent.AddChild(new DagNode<string, decimal>("child"), 0.25m);

      var percentages = new Dag<string, decimal>(parent)
        .OracleSelectEdges((owningParent, edge) => $"{owningParent.Value} owns {(int)(edge.Value * 100)}%");

      Assert.AreEqual("parent", percentages.Sources[0].Value);
      Assert.AreEqual("parent owns 25%", percentages.Sources[0].ChildEdges[0].Value);
    }

    [TestMethod]
    public void Select_CarriesEdgePayloadsUnchanged()
    {
      var parent = new DagNode<string, decimal>("parent");
      parent.AddChild(new DagNode<string, decimal>("child"), 0.25m);

      var mapped = new Dag<string, decimal>(parent).OracleSelect(node => node.Value.ToUpperInvariant());

      Assert.AreEqual(0.25m, mapped.Sources[0].ChildEdges[0].Value);
    }

    [TestMethod]
    public void SourcefixScan_PathCounts_SharedNodeMergesBothInflows()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      // Number of root-to-node paths: 1 at roots, sum of parent path counts elsewhere.
      var pathCounts = dag.OracleSourcefixScan<string, int, int>(
        (node, parentAccumulations) =>
          parentAccumulations.Count == 0 ? 1 : parentAccumulations.Sum());

      var sharedClone = pathCounts.Sources[0].Children[0].Children[0];
      Assert.AreEqual(1, pathCounts.Sources[0].Value);
      Assert.AreEqual(1, pathCounts.Sources[0].Children[0].Value);
      Assert.AreEqual(2, sharedClone.Value); // one path through each of left/right
    }

    [TestMethod]
    public void SourcefixScan_CopiesTheAccumulationDownEveryOutEdge()
    {
      // A scan COPIES (unlike SourcefixAllocate, which splits): both children see the root's full value.
      var root = new DagNode<int, int>(10);
      root.AddChild(0);
      root.AddChild(0);

      var accumulated = new Dag<int, int>(root).OracleSourcefixScan<int, int, int>(
        (node, parentAccumulations) => node.Value + parentAccumulations.Sum());

      Assert.AreEqual(10, accumulated.Sources[0].Children[0].Value);
      Assert.AreEqual(10, accumulated.Sources[0].Children[1].Value);
    }

    [TestMethod]
    public void SinkfixScan_ReturnsShapeIsomorphicResults()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      // Unfolded (per-use) subtree node count, as a composable dag instead of a dictionary.
      var subtreeNodeCounts = dag.OracleSinkfixScan<string, int, int>(
        (node, childResults) => 1 + childResults.Sum());

      Assert.AreEqual(4, subtreeNodeCounts.GetTopologicalOrder().Count);
      Assert.AreEqual(5, subtreeNodeCounts.Sources[0].Value);
      Assert.AreEqual(1, subtreeNodeCounts.Sources[0].Children[0].Children[0].Value);
    }

    [TestMethod]
    public void Scans_Compose()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      // Depth-ish scan, then a select over the scan's result dag.
      var longestDepths = dag.OracleSourcefixScan<string, int, int>(
        (node, parentAccumulations) =>
          parentAccumulations.Count == 0 ? 0 : parentAccumulations.Max() + 1);
      var labels = longestDepths.OracleSelect(node => $"depth:{node.Value}");

      Assert.AreEqual("depth:0", labels.Sources[0].Value);
      Assert.AreEqual("depth:2", labels.Sources[0].Children[0].Children[0].Value);
    }

    [TestMethod]
    public void PruneBefore_RemovesMatchedNode_AndItsExclusiveDescendants()
    {
      var apex = new DagNode<string, int>("apex");
      var middle = apex.AddChild("middle");
      middle.AddChild("leaf");

      var pruned = new Dag<string, int>(apex).OraclePruneBefore(node => node.Value == "middle");

      var survivors = pruned.GetTopologicalOrder();
      Assert.AreEqual(1, survivors.Count);
      Assert.AreEqual("apex", survivors[0].Value);
      Assert.AreEqual(0, survivors[0].Children.Count);
    }

    [TestMethod]
    public void PruneBefore_SharedDescendantSurvivesViaTheOtherPath()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      var pruned = dag.OraclePruneBefore(node => node.Value == "left");

      var survivorValues = pruned.GetTopologicalOrder().Select(node => node.Value).ToList();
      CollectionAssert.AreEquivalent(new[] { "apex", "right", "shared" }, survivorValues);

      var apexClone = pruned.Sources[0];
      Assert.AreEqual(1, apexClone.Children.Count);
      Assert.AreEqual("right", apexClone.Children[0].Value);
      Assert.AreEqual("shared", apexClone.Children[0].Children[0].Value);
    }

    [TestMethod]
    public void PruneBefore_AllPathsPruned_SharedDescendantVanishes()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      var pruned = dag.OraclePruneBefore(node => node.Value == "left" || node.Value == "right");

      var survivorValues = pruned.GetTopologicalOrder().Select(node => node.Value).ToList();
      CollectionAssert.AreEqual(new[] { "apex" }, survivorValues);
    }

    [TestMethod]
    public void PruneBefore_PrunedRoot_DropsItsWholeComponent()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      var pruned = dag.OraclePruneBefore(node => node.Value == "apex");

      Assert.AreEqual(0, pruned.Sources.Count);
      Assert.AreEqual(0, pruned.GetTopologicalOrder().Count);
    }

    [TestMethod]
    public void PruneBefore_PredicateNeverRunsOnAlreadySeveredNodes()
    {
      var apex = new DagNode<string, int>("apex");
      var middle = apex.AddChild("middle");
      middle.AddChild("leaf");

      var evaluatedValues = new List<string>();

      new Dag<string, int>(apex).OraclePruneBefore(node =>
      {
        evaluatedValues.Add(node.Value);
        return node.Value == "middle";
      });

      CollectionAssert.AreEquivalent(new[] { "apex", "middle" }, evaluatedValues);
    }

    [TestMethod]
    public void PruneBefore_LeavesTheSourceUntouched()
    {
      var (dag, apex, _, _, _) = BuildDiamond();

      dag.OraclePruneBefore(node => true);

      Assert.AreEqual(2, apex.Children.Count);
      Assert.AreEqual(4, dag.GetTopologicalOrder().Count);
    }

    [TestMethod]
    public void PruneAfter_KeepsMatchedNodeAsLeaf()
    {
      var apex = new DagNode<string, int>("apex");
      var middle = apex.AddChild("middle");
      middle.AddChild("leaf");

      var pruned = new Dag<string, int>(apex).OraclePruneAfter(node => node.Value == "middle");

      var survivorValues = pruned.GetTopologicalOrder().Select(node => node.Value).ToList();
      CollectionAssert.AreEqual(new[] { "apex", "middle" }, survivorValues);
      Assert.AreEqual(0, pruned.Sources[0].Children[0].Children.Count);
    }

    [TestMethod]
    public void PruneAfter_FormerChildSurvivesViaTheOtherParent()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      var pruned = dag.OraclePruneAfter(node => node.Value == "left");

      var survivorValues = pruned.GetTopologicalOrder().Select(node => node.Value).ToList();
      CollectionAssert.AreEquivalent(new[] { "apex", "left", "right", "shared" }, survivorValues);

      var apexClone = pruned.Sources[0];
      var leftClone = apexClone.Children[0];
      var rightClone = apexClone.Children[1];
      Assert.AreEqual(0, leftClone.Children.Count); // left kept, but as a leaf

      var sharedClone = rightClone.Children[0];
      Assert.AreEqual("shared", sharedClone.Value);
      Assert.AreEqual(0, sharedClone.Children.Count);
      Assert.AreEqual(1, sharedClone.Parents.Count); // shared's only surviving in-edge is right's
    }

    [TestMethod]
    public void PruneAfter_MatchedRoot_KeepsJustTheRoot()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      var pruned = dag.OraclePruneAfter(node => node.Value == "apex");

      var survivorValues = pruned.GetTopologicalOrder().Select(node => node.Value).ToList();
      CollectionAssert.AreEqual(new[] { "apex" }, survivorValues);
    }
  }
}
