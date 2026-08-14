using Copse;
using Copse.Core;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// The capstone, distilled (UC-32): the minimum spanning subtree of the target nodes --
    /// every node on a path between targets, re-rooted at their lowest common ancestor --
    /// returned as a WALKER standing at the spanning root. Result-typed because the
    /// operation is partial exactly twice, and each miss is a fact: NO TARGETS (the
    /// spanning subtree of nothing is nothing -- guarded here, where the semantics live,
    /// not inside a seedless fold's vocabulary-free exception) and DISJOINT TREES (targets
    /// in different trees of a forest have no common ancestor). One target is not a miss:
    /// its spanning subtree is the node alone.
    ///
    /// <para>The result stands on a NEW capture of the spanning subtree (preorder,
    /// O(kept)): its handles are that capture's own ordinals, NOT the source's -- the
    /// per-capture clause. The construction composes shipped pieces end to end:
    /// walker-climbed LCA folding (stances in, stance out; the kept-set falls out of the
    /// same climbs), the severed re-root, the HANDLE-DECORATED STREAM for the membership
    /// clamp (Extend stamps every node with its own (handle, value) pair, PruneBefore cuts
    /// off-path subtrees in handle-space -- membership is downward-closed, so prune
    /// semantics are exactly right -- and Select projects back), and one Materialize. A
    /// future membership LENS makes the clamp adjacency-side and this zero-copy; the
    /// semantics are fixed here. Target handles are presumed to be the source's own (the
    /// foreign-handle clause). Sync-only for now, like the lens family it lives beside.</para>
    /// </summary>
    public static TreeWalkerResult<TValue, int> SpanningSubtree<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      IEnumerable<THandle> targets)
    {
      var targetWalkers = targets.Select(handle => source.WalkerAt(handle)).ToList();

      if (targetWalkers.Count == 0)
        return default;

      var spanningRootResult = new TreeWalkerResult<TValue, THandle>(targetWalkers[0]);

      for (var index = 1; index < targetWalkers.Count; index++)
      {
        spanningRootResult = LowestCommonAncestor(spanningRootResult.Walker, targetWalkers[index]);

        if (!spanningRootResult.HasWalker)
          return default;
      }

      var spanningRoot = spanningRootResult.Walker;

      // The kept-set: every node on a target-to-root path, recorded by the climbs. Each
      // climb stops at the first already-kept ancestor (shared path segments are walked
      // once), and cannot walk past a root: the spanning root is a proven ancestor of
      // every target, and it is seeded first.
      var keptHandles = new HashSet<THandle> { spanningRoot.Focus };

      foreach (var target in targetWalkers)
      {
        var stance = target;

        while (!keptHandles.Contains(stance.Focus))
        {
          keptHandles.Add(stance.Focus);
          stance = stance.MoveToParent().Walker;
        }
      }

      var clamped = spanningRoot.Subtree()
        .Extend((terrain, handle) => new HandleAndValue<THandle, TValue>(handle, terrain.GetValue(handle)))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Value);

      return clamped.Materialize(BufferLayout.Preorder).GetRootWalker();
    }

    // The binary LCA, walker-first and result-typed (the axis wave will promote this to a
    // public extension; the spanning fold is its first consumer): collect one stance's
    // root path into a handle set, climb the other until the first membership hit. The
    // miss -- disjoint trees -- is a fact, never a default walker. Same-terrain is
    // presumed (the walkers' terrain is private even to this assembly; the check becomes
    // possible when the axis wave lands in the walker's own assembly).
    private static TreeWalkerResult<TValue, THandle> LowestCommonAncestor<TValue, THandle>(
      TreeWalker<TValue, THandle> first,
      TreeWalker<TValue, THandle> second)
    {
      var firstRootPath = new HashSet<THandle>();
      var stance = first;

      while (true)
      {
        firstRootPath.Add(stance.Focus);

        var up = stance.MoveToParent();
        if (!up.HasWalker)
          break;

        stance = up.Walker;
      }

      var candidate = second;

      while (!firstRootPath.Contains(candidate.Focus))
      {
        var up = candidate.MoveToParent();
        if (!up.HasWalker)
          return default;

        candidate = up.Walker;
      }

      return new TreeWalkerResult<TValue, THandle>(candidate);
    }
  }
}
