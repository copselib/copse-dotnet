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
    /// foreign-handle clause).</para>
    /// </summary>
    public static async ValueTask<AsyncTreeWalkerResult<TValue, int>> SpanningSubtreeAsync<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      IEnumerable<THandle> targets)
    {
      var targetWalkers = targets.Select(handle => source.GetTreeWalkerAt(handle)).ToList();

      if (targetWalkers.Count == 0)
        return default;

      var spanningRootResult = new AsyncTreeWalkerResult<TValue, THandle>(targetWalkers[0]);

      for (var index = 1; index < targetWalkers.Count; index++)
      {
        spanningRootResult = await LowestCommonAncestorAsync(spanningRootResult.Walker, targetWalkers[index]).ConfigureAwait(false);

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
          stance = (await stance.MoveToParentAsync().ConfigureAwait(false)).Walker;
        }
      }

      var clamped = spanningRoot.Subtree()
        .Extend((terrain, handle) => PairHandleWithValueAsync(terrain, handle))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Value);

      return await clamped.Materialize(BufferLayout.Preorder).TryGetTreeWalkerAtRootIndexAsync().ConfigureAwait(false);
    }

    // The handle-decorated stream's stamp, as a named observer so both colors read the same:
    // every node paired with its own handle, the membership clamp's coordinate system.
    private static async ValueTask<HandleAndValue<THandle, TValue>> PairHandleWithValueAsync<TValue, THandle>(
      IAsyncTreeTerrain<TValue, THandle> terrain,
      THandle handle)
      => new HandleAndValue<THandle, TValue>(handle, await terrain.GetValueAsync(handle).ConfigureAwait(false));

    // The binary LCA, walker-first and result-typed (the axis wave will promote this to a
    // public extension; the spanning fold is its first consumer): collect one stance's
    // root path into a handle set, climb the other until the first membership hit. The
    // miss -- disjoint trees -- is a fact, never a default walker. Same-terrain is
    // presumed (the walkers' terrain is private even to this assembly; the check becomes
    // possible when the axis wave lands in the walker's own assembly).
    private static async ValueTask<AsyncTreeWalkerResult<TValue, THandle>> LowestCommonAncestorAsync<TValue, THandle>(
      AsyncTreeWalker<TValue, THandle> first,
      AsyncTreeWalker<TValue, THandle> second)
    {
      var firstRootPath = new HashSet<THandle>();
      var stance = first;

      while (true)
      {
        firstRootPath.Add(stance.Focus);

        var up = await stance.MoveToParentAsync().ConfigureAwait(false);
        if (!up.HasWalker)
          break;

        stance = up.Walker;
      }

      var candidate = second;

      while (!firstRootPath.Contains(candidate.Focus))
      {
        var up = await candidate.MoveToParentAsync().ConfigureAwait(false);
        if (!up.HasWalker)
          return default;

        candidate = up.Walker;
      }

      return new AsyncTreeWalkerResult<TValue, THandle>(candidate);
    }
  }
}
