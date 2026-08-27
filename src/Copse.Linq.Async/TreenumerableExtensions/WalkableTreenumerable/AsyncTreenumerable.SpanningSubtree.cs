using Copse;
using Copse.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The capstone, distilled (UC-32): the minimum spanning subtree of the target nodes --
    /// every node on a path between targets, re-rooted at their lowest common ancestor --
    /// returned as a WALKER standing at the spanning root. Result-typed because the
    /// operation is partial exactly once: NO TARGETS (the spanning subtree of nothing is
    /// nothing -- guarded here, where the semantics live, not inside a seedless fold's
    /// vocabulary-free exception). Targets in different trees are NOT a miss: their common
    /// ancestor is the unfocused stance, and the answer is the spanning FOREST --
    /// one spanning subtree per touched tree, the returned walker unfocused above them. One target is not a miss either: its spanning subtree is the node alone.
    ///
    /// <para>The result stands on a NEW capture of the spanning subtree (preorder,
    /// O(kept)): its handles are that capture's own ordinals, NOT the source's -- the
    /// per-capture clause. The construction composes shipped pieces end to end:
    /// walker-climbed LCA folding (stances in, stance out; climbs terminate at the unfocused stance,
    /// so the fold is total; the kept-set falls out of the same climbs), the hoist
    /// (<c>Subtree()</c> -- severed re-root at a node, the whole forest at the unfocused stance), the
    /// HANDLE-DECORATED STREAM for the membership clamp (Extend stamps every node with its
    /// own (handle, value) pair, PruneSubtreesWhere cuts off-path subtrees in handle-space --
    /// membership is downward-closed, so prune semantics are exactly right -- and Select
    /// projects back), and one Materialize. A future membership LENS makes the clamp
    /// adjacency-side and this zero-copy; the semantics are fixed here. Target handles are
    /// presumed to be the source's own (the foreign-handle clause).</para>
    /// </summary>
    public static async ValueTask<Option<AsyncTreeWalker<TNode, int>>> SpanningSubtreeAsync<TNode, THandle>(
      this IAsyncWalkableTreenumerable<TNode, THandle> source,
      IEnumerable<THandle> targets)
    {
      var targetList = targets.ToList();

      if (targetList.Count == 0)
        return default;

      // One knock at the door, then the jump lifts every stored handle into a stance.
      var door = await source.GetTreeWalkerAsync().ConfigureAwait(false);
      var targetWalkers = targetList.Select(handle => door.At(handle)).ToList();

      var spanningRoot = targetWalkers[0];

      for (var index = 1; index < targetWalkers.Count && spanningRoot.HasFocus; index++)
        spanningRoot = await LowestCommonAncestorAsync(spanningRoot, targetWalkers[index]).ConfigureAwait(false);

      // The kept-set: every node on a target-to-root path, recorded by the climbs. Each
      // climb stops at the first already-kept ancestor (shared path segments are walked
      // once) or at the unfocused stance -- the spanning root is a proven ancestor of every target,
      // and when it is a node it is seeded first.
      var keptHandles = new HashSet<THandle>();

      if (spanningRoot.HasFocus)
        keptHandles.Add(spanningRoot.Focus);

      foreach (var target in targetWalkers)
      {
        var stance = target;

        while (stance.HasFocus && !keptHandles.Contains(stance.Focus))
        {
          keptHandles.Add(stance.Focus);
          stance = (await stance.MoveToParentAsync().ConfigureAwait(false)).Value;
        }
      }

      var clamped = spanningRoot.Subtree()
        .Extend((topology, handle) => PairHandleWithValueAsync(topology, handle))
        .PruneSubtreesWhere(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Node);

      var capture = clamped.Materialize(BufferLayout.Preorder);

      // Disjoint targets: the spanning walker comes back unfocused --
      // its child group is the per-tree spanning roots. A node-rooted spanning stands at
      // its root, ordinal zero of the capture (the per-capture clause, pinned; the kept
      // set is nonempty by construction, so the step's answer is present).
      if (!spanningRoot.HasFocus)
        return new Option<AsyncTreeWalker<TNode, int>>(await capture.GetTreeWalkerAsync().ConfigureAwait(false));

      var captureRoot = await capture.TryGetTreeWalkerAtRootIndexAsync().ConfigureAwait(false);

      return new Option<AsyncTreeWalker<TNode, int>>(captureRoot.Value);
    }

    // The handle-decorated stream's stamp, as a named observer so both colors read the same:
    // every node paired with its own handle, the membership clamp's coordinate system.
    private static async ValueTask<HandleAndNode<THandle, TNode>> PairHandleWithValueAsync<TNode, THandle>(
      IAsyncTreeTopology<TNode, THandle> topology,
      THandle handle)
      => new HandleAndNode<THandle, TNode>(handle, await topology.GetNodeAsync(handle).ConfigureAwait(false));

    // The binary LCA, walker-first and TOTAL (the axis wave will promote this to a public
    // extension; the spanning fold is its first consumer): collect one stance's root path
    // into a handle set, climb the other until the first membership hit or the top. Two
    // nodes in different trees meet at the unfocused stance -- an answer, not a miss (the unfocused
    // stance is every node's ancestor). Same-topology is presumed. Unfocused in, unfocused
    // out: it is already every node's ancestor, so it is already the answer.
    private static async ValueTask<AsyncTreeWalker<TNode, THandle>> LowestCommonAncestorAsync<TNode, THandle>(
      AsyncTreeWalker<TNode, THandle> first,
      AsyncTreeWalker<TNode, THandle> second)
    {
      if (!first.HasFocus)
        return first;

      var firstRootPath = new HashSet<THandle>();
      var stance = first;

      while (stance.HasFocus)
      {
        firstRootPath.Add(stance.Focus);
        stance = (await stance.MoveToParentAsync().ConfigureAwait(false)).Value;
      }

      var candidate = second;

      while (candidate.HasFocus && !firstRootPath.Contains(candidate.Focus))
        candidate = (await candidate.MoveToParentAsync().ConfigureAwait(false)).Value;

      return candidate;
    }
  }
}
