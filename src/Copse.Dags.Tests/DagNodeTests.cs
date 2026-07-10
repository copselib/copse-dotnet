using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  [TestClass]
  public class DagNodeTests
  {
    [TestMethod]
    public void AddChild_LinksBothDirections()
    {
      var parent = new DagNode<string, int>("parent");
      var child = parent.AddChild("child");

      Assert.AreEqual(1, parent.Children.Count);
      Assert.AreSame(child, parent.Children[0]);
      Assert.AreEqual(1, child.Parents.Count);
      Assert.AreSame(parent, child.Parents[0]);
    }

    [TestMethod]
    public void AddChild_ReturnsChild_SoSpinesChainDownward()
    {
      var root = new DagNode<string, int>("root");
      var grandchild = root.AddChild("child").AddChild("grandchild");

      Assert.AreEqual("child", root.Children[0].Value);
      Assert.AreSame(grandchild, root.Children[0].Children[0]);
    }

    [TestMethod]
    public void SharedChild_HasOneParentEntryPerInEdge()
    {
      var left = new DagNode<string, int>("left");
      var right = new DagNode<string, int>("right");
      var shared = new DagNode<string, int>("shared");

      left.AddChild(shared);
      right.AddChild(shared);

      Assert.AreEqual(2, shared.Parents.Count);
      Assert.AreSame(left, shared.Parents[0]);
      Assert.AreSame(right, shared.Parents[1]);
    }

    [TestMethod]
    public void ParallelEdges_ArePermitted_AndDuplicateTheBackLink()
    {
      var parent = new DagNode<string, int>("parent");
      var child = new DagNode<string, int>("child");

      parent.AddChild(child);
      parent.AddChild(child);

      Assert.AreEqual(2, parent.Children.Count);
      Assert.AreEqual(2, child.Parents.Count);
      Assert.AreSame(parent, child.Parents[0]);
      Assert.AreSame(parent, child.Parents[1]);
    }

    [TestMethod]
    public void RemoveChild_RemovesOneEdgeAndOneBackLink()
    {
      var parent = new DagNode<string, int>("parent");
      var child = new DagNode<string, int>("child");

      parent.AddChild(child);
      parent.AddChild(child);

      Assert.IsTrue(parent.RemoveChild(child));

      Assert.AreEqual(1, parent.Children.Count);
      Assert.AreEqual(1, child.Parents.Count);

      Assert.IsTrue(parent.RemoveChild(child));
      Assert.IsFalse(parent.RemoveChild(child));

      Assert.AreEqual(0, parent.Children.Count);
      Assert.AreEqual(0, child.Parents.Count);
    }

    [TestMethod]
    public void SortChildrenBy_IsPerParent_SharedChildOrderUnderOtherParentsUntouched()
    {
      var first = new DagNode<int, int>(0);
      var second = new DagNode<int, int>(0);
      var childA = new DagNode<int, int>(1);
      var childB = new DagNode<int, int>(2);

      // Same two shared children, opposite insertion order under each parent.
      first.AddChild(childB);
      first.AddChild(childA);
      second.AddChild(childB);
      second.AddChild(childA);

      first.SortChildrenBy(child => child.Value);

      Assert.AreSame(childA, first.Children[0]);
      Assert.AreSame(childB, first.Children[1]);

      // The other parent's edge order is untouched.
      Assert.AreSame(childB, second.Children[0]);
      Assert.AreSame(childA, second.Children[1]);
    }

    [TestMethod]
    public void AddChild_WithEdgeValue_PayloadVisibleFromBothEnds()
    {
      var parent = new DagNode<string, decimal>("parent");
      var child = parent.AddChild(new DagNode<string, decimal>("child"), 0.60m);

      Assert.AreEqual(0.60m, parent.ChildEdges[0].Value);
      Assert.AreSame(child, parent.ChildEdges[0].Child);
      Assert.AreEqual(0.60m, child.ParentEdges[0].Value);
      Assert.AreSame(parent, child.ParentEdges[0].Parent);
    }

    [TestMethod]
    public void SortChildEdgesBy_Payload_MovesPayloadsWithTheirEdges()
    {
      var parent = new DagNode<string, decimal>("parent");
      var minority = parent.AddChild(new DagNode<string, decimal>("minority"), 0.20m);
      var majority = parent.AddChild(new DagNode<string, decimal>("majority"), 0.80m);

      parent.SortChildEdgesBy(edge => -edge.Value); // descending ownership

      Assert.AreSame(majority, parent.Children[0]);
      Assert.AreEqual(0.80m, parent.ChildEdges[0].Value);
      Assert.AreSame(minority, parent.Children[1]);
      Assert.AreEqual(0.20m, parent.ChildEdges[1].Value);
    }

    [TestMethod]
    public void SortChildrenBy_IsStable_EqualKeysKeepInsertionOrder()
    {
      var parent = new DagNode<int, int>(0);
      var firstWithEqualKey = parent.AddChild(5);
      var secondWithEqualKey = parent.AddChild(5);
      var smallest = parent.AddChild(1);

      parent.SortChildrenBy(child => child.Value);

      Assert.AreSame(smallest, parent.Children[0]);
      Assert.AreSame(firstWithEqualKey, parent.Children[1]);
      Assert.AreSame(secondWithEqualKey, parent.Children[2]);
    }
  }
}
