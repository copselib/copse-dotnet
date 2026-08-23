using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The acquisition scans: the handle space is ENUMERATED, never computed from values -- every
  // handle exactly once (the diamond's shared venture is one row however many paths reach it:
  // set semantics, the dag axis default), paired with the value it labels, over every citizen.
  // Searches are consumer LINQ over the rows (the search law); the empty sequence is the miss,
  // and the sentinel trap is pinned as a warning: FirstOrDefault over ordinal handles returns
  // 0, a REAL handle.
  [TestClass]
  public class DagWalkerAcquisitionTests
  {
    [TestMethod]
    public void GetHandles_YieldsEveryHandleExactlyOnce()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          var handles = walkable.GetHandles().ToList();
          Assert.AreEqual(handles.Count, handles.Distinct().Count(), $"no handle twice [{dagName}/{name}]");
          Assert.AreEqual(walkable.GetTopologicalOrder().Count(), handles.Count, $"every node once [{dagName}/{name}]");
        }

      Assert.AreEqual(4, DagWalkerCorpus.Diamond().GetHandles().Count(), "builder: the venture once");
      Assert.AreEqual(4, new FamilyFreeDag().GetHandles().Count(), "foreign: the venture once");
    }

    [TestMethod]
    public void GetHandlesWithValues_YieldsTheRowsOfTheLabeling()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var rows = walkable.GetHandlesWithValues().ToList();
        CollectionAssert.AreEquivalent(new[] { "apex", "left", "right", "venture" }, rows.Select(row => row.Value).ToList(), name);
        foreach (var row in rows)
          Assert.AreEqual(row.Value, walkable.GetDagWalkerAt(row.Handle).GetValue(), $"the pairing is the labeling [{name}]");
      }
    }

    [TestMethod]
    public void TheDoor_EveryCitizen_AndTheEmptyDagAnswers()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          Assert.IsNotNull(walkable.GetDagWalker().Topology, $"{dagName}/{name}");
    }

    [TestMethod]
    public void SearchesAreConsumerLinq_AndTheEmptySequenceIsTheMiss()
    {
      var walkable = DagWalkerCorpus.Diamond().Materialize();
      var venture = walkable.GetHandlesWithValues().Where(row => row.Value == "venture").Select(row => row.Handle).ToList();
      Assert.AreEqual(1, venture.Count);
      Assert.AreEqual("venture", walkable.GetDagWalkerAt(venture[0]).GetValue());

      var missing = walkable.GetHandlesWithValues().Where(row => row.Value == "nobody").Select(row => row.Handle).ToList();
      Assert.AreEqual(0, missing.Count, "the empty sequence is the miss");

      // The sentinel trap: default(int) is handle 0, the apex -- a real node.
      var trapped = walkable.GetHandlesWithValues().Where(row => row.Value == "nobody").Select(row => row.Handle).FirstOrDefault();
      Assert.AreEqual("apex", walkable.GetDagWalkerAt(trapped).GetValue(), "FirstOrDefault over ordinal handles masquerades as the apex");
    }
  }
}
