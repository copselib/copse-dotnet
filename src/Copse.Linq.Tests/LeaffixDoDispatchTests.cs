using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The impure sibling-complete upward pass (SPIKE, feature/do-scan): LeaffixDispatch's Do
  // twin. The battery pins the (survey, store) contracts -- survey pure and value-flavored,
  // store exactly once per node PER BUILD (both leaffix tiers are captures, so effects fire
  // once at first drain and replays never re-fire) -- plus pass-through, total coverage
  // (leaves via seed/selector, internal nodes via survey), and preorder store order.
  [TestClass]
  public class LeaffixDoDispatchTests
  {
    private sealed class Entity
    {
      public string Name;
      public int OwnValue;
      public int SubtreeTotal;
      public int Stores;

      public override string ToString() => Name;
    }

    //  a-1(b-2(d-4,e-5),c-3): subtree totals d=4, e=5, b=11, c=3, a=15.
    private static ITreenumerableBuffer<Entity> Structure() =>
      TreeSerializer
        .DeserializeDepthFirstTree("a-1(b-2(d-4,e-5),c-3)", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], OwnValue = int.Parse(parts[1]) };
        })
        .Materialize();

    private static ITreenumerableBuffer<Entity> Rollup(ITreenumerable<Entity> tree) =>
      tree.LeaffixDoDispatch(
        leaf => leaf.OwnValue,
        (node, children) =>
        {
          var total = node.OwnValue;
          foreach (var childTotal in children)
            total += childTotal;
          return total;
        },
        (entity, total) => { entity.SubtreeTotal = total; entity.Stores++; });

    [TestMethod]
    public void RollupLandsOnTheEntities_LeavesViaSelectorInternalViaSurvey()
    {
      var tree = Structure();

      Rollup(tree).PreorderTraversal().ToArray();

      var byName = tree.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(15, byName["a"].SubtreeTotal);
      Assert.AreEqual(11, byName["b"].SubtreeTotal);
      Assert.AreEqual(3, byName["c"].SubtreeTotal);
      Assert.AreEqual(4, byName["d"].SubtreeTotal);
      Assert.AreEqual(5, byName["e"].SubtreeTotal);

      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 1),
        "store fires exactly once per node -- leaves and internal nodes alike");
    }

    [TestMethod]
    public void SeedForm_EveryLeafStartsAtTheSeed_TheLeafCountShape()
    {
      var tree = Structure();

      tree
        .LeaffixDoDispatch(
          1,
          (node, children) =>
          {
            var count = 0;
            foreach (var childCount in children)
              count += childCount;
            return count;
          },
          (entity, count) => entity.SubtreeTotal = count)
        .PreorderTraversal()
        .ToArray();

      var byName = tree.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(3, byName["a"].SubtreeTotal, "three leaves under a");
      Assert.AreEqual(2, byName["b"].SubtreeTotal);
      Assert.AreEqual(1, byName["c"].SubtreeTotal, "a leaf counts itself via the seed");
    }

    [TestMethod]
    public void EffectsAreDeferredToTheFirstDrain_AndNeverRefire()
    {
      var tree = Structure();

      var rollup = Rollup(tree);
      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 0),
        "deferred-once: no effects before the first acquisition");

      rollup.PreorderTraversal().ToArray();
      rollup.LevelOrderTraversal().ToArray();
      rollup.PreorderTraversal().ToArray();

      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 1),
        "the build ran once; replays never re-fire the effects");
    }

    [TestMethod]
    public void NodesPassThroughUnchanged_BothDimensions()
    {
      var tree = Structure();
      var rollup = Rollup(tree);

      CollectionAssert.AreEqual(tree.PreorderTraversal().ToArray(), rollup.PreorderTraversal().ToArray());
      CollectionAssert.AreEqual(tree.LevelOrderTraversal().ToArray(), rollup.LevelOrderTraversal().ToArray());
    }

    [TestMethod]
    public void StoreRunsInPreorder_AfterTheFoldCompletes()
    {
      var tree = Structure();
      var storeOrder = new List<string>();

      tree
        .LeaffixDoDispatch(
          0,
          (node, children) => 0,
          (entity, _) => storeOrder.Add(entity.Name))
        .PreorderTraversal()
        .ToArray();

      CollectionAssert.AreEqual(new[] { "a", "b", "d", "e", "c" }, storeOrder);
    }

    [TestMethod]
    public void PositionalSelector_SeedsLeavesByPosition()
    {
      var tree = Structure();

      tree
        .LeaffixDoDispatch(
          (leaf, position) => position.Depth * 100 + position.SiblingIndex,
          (node, children) =>
          {
            var sum = 0;
            foreach (var childValue in children)
              sum += childValue;
            return sum;
          },
          (entity, value) => entity.SubtreeTotal = value)
        .PreorderTraversal()
        .ToArray();

      var byName = tree.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(200, byName["d"].SubtreeTotal, "depth 2, sibling 0");
      Assert.AreEqual(201, byName["e"].SubtreeTotal, "depth 2, sibling 1");
      Assert.AreEqual(101, byName["c"].SubtreeTotal, "depth 1, sibling 1 -- a leaf");
      Assert.AreEqual(401, byName["b"].SubtreeTotal, "sum of its leaves");
    }
  }
}
