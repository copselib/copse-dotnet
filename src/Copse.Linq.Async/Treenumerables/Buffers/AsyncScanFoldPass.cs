using Copse.Core;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  /// <summary>
  /// The one fold pass all product variants of a scan share (the at-most-once rule,
  /// SELECT_INTO_CAPTURES_DESIGN.md): ComposeSelect forks the CITIZEN, never the source
  /// walk, so the walk's artifacts -- per-node values, the skeleton, the accumulations --
  /// are built at most once and every variant owns only its finisher zip. The build thunk
  /// is released once run, so when the last unbuilt variant builds (or dies), the raw
  /// arrays go with it and each survivor holds only its own product store plus the SHARED
  /// skeleton.
  ///
  /// <para>THE FIRST-CALLER FUSION (the guard-rail fix, 2026-08-17): the pass can spell the
  /// canonical pairing in its own type parameters, so when the FIRST variant to build is
  /// the canonical one, the build writes the pair products INLINE in its fold loop -- no
  /// second pass, no value re-reads through the ValueAt delegate; the un-composed spelling
  /// pays exactly what it paid before the pass existed. A composed first caller declines
  /// (its 1-wide product zips from the artifacts, and the pair array is never built); a
  /// canonical variant arriving AFTER a composed build finds no fused pairs and zips like
  /// any sibling -- the only profile that pays the two-pass shape, and the rarest.</para>
  ///
  /// <para>Single-threaded by contract, like the builds it runs.</para>
  /// </summary>
  internal sealed class AsyncScanFoldPass<TSource, TAccumulate>
  {
    public AsyncScanFoldPass(
      Func<bool, ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)>> build)
    {
      _Build = build;
    }

    private Func<bool, ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)>> _Build;
    private ScanFoldArtifacts<TSource, TAccumulate> _Artifacts;
    private NodeAccumulation<TSource, TAccumulate>[] _FusedPairProducts;

    /// <summary>
    /// Run the build if it has not run (asking it to fuse pair products iff the caller is
    /// the canonical variant); afterwards, the artifacts -- and the fused pair array iff
    /// the FIRST builder asked for it (a later canonical caller finding null zips instead).
    /// </summary>
    public async ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)> EnsureAsync(
      bool fuseCanonicalPairing)
    {
      if (_Build != null)
      {
        (_Artifacts, _FusedPairProducts) = await _Build(fuseCanonicalPairing).ConfigureAwait(false);
        _Build = null;
      }

      return (_Artifacts, _FusedPairProducts);
    }
  }

  /// <summary>
  /// A completed fold pass: the value reader (a delegate, so an in-place fold can read the
  /// receiver's own store without copying its values), the accumulations, and the skeleton
  /// -- one immutable int[] deliberately SHARED by every product store zipped from this
  /// pass (subtree sizes are identical across product variants by construction).
  /// </summary>
  internal readonly struct ScanFoldArtifacts<TSource, TAccumulate>
  {
    public ScanFoldArtifacts(Func<int, TSource> valueAt, TAccumulate[] accumulates, int[] subtreeSizes)
    {
      ValueAt = valueAt;
      Accumulates = accumulates;
      SubtreeSizes = subtreeSizes;
    }

    public readonly Func<int, TSource> ValueAt;
    public readonly TAccumulate[] Accumulates;
    public readonly int[] SubtreeSizes;

    public int Count => Accumulates.Length;
  }
}
