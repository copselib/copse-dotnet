using Copse.Core;
using Copse.SimpleSerializer;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // The walker law suites' provider fan-out (docs/WALKABLE_CONTRACT_DESIGN.md §4): the
  // comonad laws must hold for every citizen claiming the walkable contract, not just the
  // one the PoC built on -- so each law runs over both declared capture layouts (different
  // handle SPACES; the laws are handle-agnostic) and a fresh memo, whose probes drive the
  // capture mid-race (the pull-through case). Hand-pinned ordinal expectations stay
  // preorder-only in their own tests; everything law-shaped rides this fan-out.
  internal static class WalkerLawProviders
  {
    public static IEnumerable<IWalkableTreenumerable<string, int>> Walkables(string tree)
    {
      yield return TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.Preorder);
      yield return TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.LevelOrder);
      yield return TreeSerializer.DeserializeDepthFirstTree(tree).Memoize();
    }
  }
}
