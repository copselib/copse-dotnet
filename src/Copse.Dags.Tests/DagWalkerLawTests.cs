using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The carrier's own laws (the tree family's TreeWalkerLawTests, dualized): the (topology,
  // handle) laws as typed identities on DagWalker -- extract is the value at the focus, the
  // counit after duplicate is the walker itself, duplicate commutes with every step (Store's
  // peek/seek coherence, now over three step families), the walker sees UP through the in-edge
  // group (Store, not cofree), the source door answers in bounds and misses past the last
  // source, the empty dag has no focused walker, and the steps are transpose-consistent: every
  // edge crossed down can be crossed back up, payload intact. The bodies are generic over the
  // handle type so every citizen -- buffer, skeleton, builder, foreign -- runs the same law.
  [TestClass]
  public class DagWalkerLawTests
  {


    [TestMethod]
    public void Extract_IsTheValueAtTheFocus()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          AssertExtract(walkable, $"{dagName}/{name}");

      AssertExtract(DagWalkerCorpus.Diamond(), "diamond/builder");
      AssertExtract(new FamilyFreeDag(), "diamond/foreign");
    }

    private static void AssertExtract<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, string label)
    {
      foreach (var row in walkable.GetHandlesWithValues())
      {
        var walker = walkable.GetDagWalkerAt(row.Handle);
        Assert.AreEqual(row.Value, walker.GetValue(), $"extract [{label}]");
        Assert.IsTrue(walker.TryGetValue(out var value) && value == row.Value, $"typed extract agrees [{label}]");
        Assert.AreSame(walker.Topology, walker.At(row.Handle).Topology, $"the jump keeps its door's topology [{label}]");
      }
    }

    [TestMethod]
    public void Counit_ExtractAfterDuplicate_IsTheWalkerItself()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          AssertCounit(walkable, $"{dagName}/{name}");

      AssertCounit(DagWalkerCorpus.Diamond(), "diamond/builder");
      AssertCounit(new FamilyFreeDag(), "diamond/foreign");
    }

    private static void AssertCounit<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, string label)
    {
      foreach (var handle in walkable.GetHandles())
      {
        var walker = walkable.GetDagWalkerAt(handle);
        var duplicated = walker.Duplicate();
        var extracted = duplicated.GetValue();
        Assert.AreEqual(walker.Focus, extracted.Focus, $"counit: the label at the focus is the walker there [{label}]");
        Assert.AreEqual(walker.GetValue(), extracted.GetValue(), $"counit: same value [{label}]");
        Assert.AreEqual(walker.Focus, duplicated.Focus, $"duplicate keeps the stance [{label}]");
      }
    }

    [TestMethod]
    public void Duplicate_CommutesWithSteps()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          AssertDuplicateCommutes(walkable, $"{dagName}/{name}");

      AssertDuplicateCommutes(DagWalkerCorpus.Diamond(), "diamond/builder");
      AssertDuplicateCommutes(new FamilyFreeDag(), "diamond/foreign");
    }

    private static void AssertDuplicateCommutes<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, string label)
    {
      foreach (var handle in walkable.GetHandles())
      {
        var walker = walkable.GetDagWalkerAt(handle);
        var duplicated = walker.Duplicate();

        for (var index = 0; ; index++)
        {
          var stepped = walker.MoveToChild(index);
          var steppedDuplicate = duplicated.MoveToChild(index);
          Assert.AreEqual(stepped.HasValue, steppedDuplicate.HasValue, $"child step parity [{label}]");
          if (!stepped.HasValue)
            break;
          Assert.AreEqual(stepped.Value.Focus, steppedDuplicate.Value.GetValue().Focus, $"duplicate then step ≡ step then duplicate (child {index}) [{label}]");
          Assert.AreEqual(stepped.Edge, steppedDuplicate.Edge, $"the edge crossed agrees [{label}]");
        }

        for (var index = 0; ; index++)
        {
          var stepped = walker.MoveToParent(index);
          var steppedDuplicate = duplicated.MoveToParent(index);
          Assert.AreEqual(stepped.HasValue, steppedDuplicate.HasValue, $"parent step parity [{label}]");
          if (!stepped.HasValue)
            break;
          Assert.AreEqual(stepped.Value.HasFocus, steppedDuplicate.Value.HasFocus, $"unfocused parity [{label}]");
          if (stepped.Value.HasFocus)
            Assert.AreEqual(stepped.Value.Focus, steppedDuplicate.Value.GetValue().Focus, $"duplicate then step ≡ step then duplicate (parent {index}) [{label}]");
        }
      }
    }

    [TestMethod]
    public void TheWalkerSeesUp_ThroughTheInEdgeGroup()
    {
      // Store, not cofree: from the venture, BOTH parents are reachable, in discovery order,
      // each with the payload of the edge climbed.
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
        AssertSeesUp(walkable, name);

      AssertSeesUp(DagWalkerCorpus.Diamond(), "builder");
      AssertSeesUp(new FamilyFreeDag(), "foreign");
    }

    private static void AssertSeesUp<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, string label)
    {
      var venture = walkable.GetDagWalkerAt(walkable.GetHandlesWithValues().Single(row => row.Value == "venture").Handle);
      var viaLeft = venture.MoveToParent(0);
      var viaRight = venture.MoveToParent(1);
      Assert.AreEqual("left", viaLeft.Value.GetValue(), $"in-edge 0 [{label}]");
      Assert.AreEqual(0.70m, viaLeft.Edge, $"in-edge 0 payload [{label}]");
      Assert.AreEqual("right", viaRight.Value.GetValue(), $"in-edge 1 [{label}]");
      Assert.AreEqual(0.30m, viaRight.Edge, $"in-edge 1 payload [{label}]");
      Assert.IsFalse(venture.MoveToParent(2).HasValue, $"past the in-edge group [{label}]");
      Assert.AreEqual("apex", viaLeft.Value.MoveToParent(0).Value.GetValue(), $"and up again [{label}]");
    }

    [TestMethod]
    public void Steps_AreTransposeConsistent_EveryEdgeCrossedDownCrossesBackUp()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          AssertTransposeConsistent(walkable, $"{dagName}/{name}");

      AssertTransposeConsistent(DagWalkerCorpus.Diamond(), "diamond/builder");
      AssertTransposeConsistent(DagWalkerCorpus.SharedLeaf(), "sharedLeaf/builder");
      AssertTransposeConsistent(new FamilyFreeDag(), "diamond/foreign");
    }

    private static void AssertTransposeConsistent<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, string label)
    {
      var comparer = EqualityComparer<THandle>.Default;

      foreach (var handle in walkable.GetHandles())
      {
        var walker = walkable.GetDagWalkerAt(handle);

        for (var outEdgeIndex = 0; ; outEdgeIndex++)
        {
          var down = walker.MoveToChild(outEdgeIndex);
          if (!down.HasValue)
            break;

          var matched = false;
          for (var inEdgeIndex = 0; ; inEdgeIndex++)
          {
            var up = down.Value.MoveToParent(inEdgeIndex);
            if (!up.HasValue || !up.Value.HasFocus)
              break;
            if (comparer.Equals(up.Value.Focus, handle) && up.Edge == down.Edge)
            {
              matched = true;
              break;
            }
          }

          Assert.IsTrue(matched, $"out-edge {outEdgeIndex} of {walker.GetValue()} has a matching in-edge at its child [{label}]");
        }
      }
    }

    [TestMethod]
    public void TheSourceDoor_AnswersInBounds_MissesPastTheLastSource()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.TwoIslands))
      {
        Assert.AreEqual("island1", walkable.TryGetDagWalkerAtSourceIndex(0).Value.GetValue(), name);
        Assert.AreEqual("island2", walkable.TryGetDagWalkerAtSourceIndex(1).Value.GetValue(), name);
        Assert.IsFalse(walkable.TryGetDagWalkerAtSourceIndex(2).HasValue, $"past the last source [{name}]");
        Assert.AreEqual(0m, walkable.TryGetDagWalkerAtSourceIndex(0).Edge, $"the seed edge is default [{name}]");
      }

      var builder = DagWalkerCorpus.TwoIslands();
      Assert.AreEqual("island2", builder.TryGetDagWalkerAtSourceIndex(1).Value.GetValue(), "builder");
      Assert.IsFalse(builder.TryGetDagWalkerAtSourceIndex(2).HasValue, "builder: past the last source");
    }

    [TestMethod]
    public void TheEmptyDag_HasNoFocusedWalker()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Empty))
      {
        var door = walkable.GetDagWalker();
        Assert.IsFalse(door.HasFocus, $"the door lands unfocused [{name}]");
        Assert.IsFalse(door.MoveToChild(0).HasValue, $"no sources [{name}]");
        Assert.IsFalse(door.MoveToParent(0).HasValue, $"the one upward miss [{name}]");
        Assert.AreEqual(0, walkable.GetHandles().Count(), $"no rows [{name}]");
      }

      Assert.IsFalse(DagWalkerCorpus.Empty().GetDagWalker().MoveToChild(0).HasValue, "builder: no sources");
    }

    [TestMethod]
    public void Transpose_IsAnInvolution_AndSwapsTheGroups()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var apex = walkable.TryGetDagWalkerAtSourceIndex(0).Value;
        var transposed = apex.Transpose();
        Assert.AreSame(apex.Topology, transposed.Transpose().Topology, $"transpose of transpose unwraps to the same topology [{name}]");
        Assert.AreEqual(apex.Focus, transposed.Focus, $"the focus stays [{name}]");
        Assert.IsFalse(transposed.MoveToChild(0).HasValue, $"apex has no out-edges in the transpose [{name}]");
        Assert.AreEqual("left", transposed.MoveToParent(0).Value.GetValue(), $"apex's in-edge group in the transpose is its out-edge group [{name}]");
        Assert.AreEqual(0.60m, transposed.MoveToParent(0).Edge, $"payloads ride unchanged [{name}]");
        Assert.AreEqual("venture", transposed.MoveToSource(0).Value.GetValue(), $"the transpose's sources are the sinks [{name}]");
        Assert.IsFalse(transposed.MoveToSource(1).HasValue, $"one sink [{name}]");
        Assert.IsFalse(walkable.GetDagWalker().Transpose().HasFocus, $"the unfocused stance transposes to the unfocused stance [{name}]");
      }
    }
  }
}
