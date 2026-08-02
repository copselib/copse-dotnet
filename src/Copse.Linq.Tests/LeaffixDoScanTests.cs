using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The impure leaffix fold (SPIKE, feature/do-scan): LeaffixScan's Do twin, sugar over
  // LeaffixDoDispatch (both leaffix tiers share the capture cost class -- the pure pair's
  // delegation, legitimately mirrored). The battery pins the fold semantics, both accumulator
  // flavors, and the ONCE-per-build effect contract -- the deliberate contrast with the
  // streaming RootfixDoScan's per-drain refire, both derived from the same rule: effect count
  // follows the laziness class.
  [TestClass]
  public class LeaffixDoScanTests
  {
    private sealed class Entity
    {
      public string Name;
      public int OwnValue;
      public int SubtreeTotal;
      public int Stores;

      public override string ToString() => Name;
    }

    private static ITreenumerableBuffer<Entity> Structure() =>
      TreeSerializer
        .DeserializeDepthFirstTree("a-1(b-2(d-4,e-5),c-3)", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], OwnValue = int.Parse(parts[1]) };
        })
        .Materialize();

    [TestMethod]
    public void SumRollup_ValueFlavor_LandsOnTheEntities()
    {
      var tree = Structure();

      tree
        .LeaffixDoScan(
          node => node.OwnValue,
          (accumulate, childAccumulate) => accumulate + childAccumulate,
          (entity, total) => { entity.SubtreeTotal = total; entity.Stores++; })
        .PreorderTraversal()
        .ToArray();

      var byName = tree.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(15, byName["a"].SubtreeTotal);
      Assert.AreEqual(11, byName["b"].SubtreeTotal);
      Assert.AreEqual(3, byName["c"].SubtreeTotal);
      Assert.AreEqual(4, byName["d"].SubtreeTotal);
      Assert.AreEqual(5, byName["e"].SubtreeTotal);
      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 1));
    }

    [TestMethod]
    public void ContextFlavor_TheCombineSeesTheFoldingNode()
    {
      var tree = Structure();
      var foldObservations = new List<string>();

      tree
        .LeaffixDoScan(
          node => node.OwnValue,
          (node, accumulate, childAccumulate) =>
          {
            foldObservations.Add($"{node.Name}<-{childAccumulate}");
            return accumulate + childAccumulate;
          },
          (entity, total) => entity.SubtreeTotal = total)
        .PreorderTraversal()
        .ToArray();

      // d closes into b, then e; b's finished fold closes into a, then c.
      CollectionAssert.AreEqual(new[] { "b<-4", "b<-5", "a<-11", "a<-3" }, foldObservations);
    }

    [TestMethod]
    public void EffectsFireOncePerBuild_TheCaptureClassContract()
    {
      // The deliberate contrast with RootfixDoScan: same Do marker, different laziness class,
      // hence different effect count -- streaming refires per drain, a capture fires once.
      var tree = Structure();

      var rollup = tree.LeaffixDoScan(
        node => node.OwnValue,
        (accumulate, childAccumulate) => accumulate + childAccumulate,
        (entity, total) => { entity.SubtreeTotal = total; entity.Stores++; });

      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 0), "deferred to the first drain");

      rollup.PreorderTraversal().ToArray();
      rollup.LevelOrderTraversal().ToArray();
      rollup.PreorderTraversal().ToArray();

      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 1),
        "one build, one firing -- replays never re-fire");
    }

    [TestMethod]
    public void NodesPassThroughUnchanged()
    {
      var tree = Structure();

      var rollup = tree.LeaffixDoScan(
        node => node.OwnValue,
        (accumulate, childAccumulate) => accumulate + childAccumulate,
        (entity, total) => entity.SubtreeTotal = total);

      CollectionAssert.AreEqual(tree.PreorderTraversal().ToArray(), rollup.PreorderTraversal().ToArray());
      CollectionAssert.AreEqual(tree.LevelOrderTraversal().ToArray(), rollup.LevelOrderTraversal().ToArray());
    }
  }
}
