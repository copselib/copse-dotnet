using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Copse.Stores;
using Copse.TestUtils;
using Copse.Treenumerables;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // The walker law suites' provider fan-out (docs/WALKABLE_CONTRACT_DESIGN.md §4): the
  // comonad laws must hold for every citizen claiming the walkable contract, not just the
  // one the PoC built on -- so each law runs over both declared capture layouts (different
  // handle SPACES; the laws are handle-agnostic), a fresh memo whose probes drive the
  // capture mid-race (the pull-through case), and -- the foundation restatement's
  // admission (2026-08-14) -- the SKELETON-DIRECT topology: a walkable whose only substance
  // is the raw preorder store, validity-checked on the way in. The skeleton is a lawful
  // carrier representation, not an implementation detail; admitting it here certifies the
  // span schedules as extends rather than only via operator conformance.
  internal static class WalkerLawProviders
  {
    public static IEnumerable<IWalkableTreenumerable<string, int>> Walkables(string tree)
    {
      yield return TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.Preorder);
      yield return TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.LevelOrder);
      yield return TreeSerializer.DeserializeDepthFirstTree(tree).Memoize();
      yield return SkeletonDirect(tree);
    }

    // The raw store, rewrapped with nothing else: the stream half decodes the store, the
    // probes ride an adjacency index over the SAME store (probes-at-birth), and no part of
    // the original pipeline survives into the topology. The store passes the validity
    // predicate first -- the laws are conditional on representation validity, so the
    // fan-out enforces the condition it depends on.
    private static IWalkableTreenumerable<string, int> SkeletonDirect(string tree)
    {
      var (hasStore, store) = ((TreenumerableBuffer<string>)TreeSerializer
        .DeserializeDepthFirstTree(tree)
        .Materialize(BufferLayout.Preorder))
        .TryGetPreorderStore();

      Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(hasStore, "a declared preorder capture must hand over its store");
      PreorderSkeletonValidity.AssertValid(store.Count, store.GetSubtreeSize);

      return new TreenumerableBuffer<string>(
        new PreorderTreenumerable<string, PreorderArrayStore<string>>(store),
        BufferLayout.Preorder,
        new PreorderAdjacencyIndex<string, PreorderArrayStore<string>>(store));
    }
  }
}
