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
      Func<ScanBuildRequest<TSource, TAccumulate>, ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)>> build)
    {
      _Build = build;
    }

    private Func<ScanBuildRequest<TSource, TAccumulate>, ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)>> _Build;
    private ScanFoldArtifacts<TSource, TAccumulate> _Artifacts;
    private NodeAccumulation<TSource, TAccumulate>[] _FusedPairProducts;
    private bool _WriterRan;

    /// <summary>
    /// Run the build if it has not run, carrying the FIRST caller's product request into the
    /// fold loop; afterwards, the artifacts plus whatever that first build fused -- the pair
    /// array for a canonical first caller, WriterRan for a composed one whose writer filled
    /// in-loop. Later callers find their own flags unserved and zip from the artifacts.
    /// </summary>
    public async ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts, bool WriterRan)> EnsureAsync(
      ScanBuildRequest<TSource, TAccumulate> request)
    {
      if (_Build != null)
      {
        (_Artifacts, _FusedPairProducts) = await _Build(request).ConfigureAwait(false);
        _WriterRan = request.ProductWriter != null && request.ProductWriter.Filled;
        _Build = null;
        return (_Artifacts, _FusedPairProducts, _WriterRan);
      }

      // The build already ran for an earlier variant: this caller's request was never seen.
      return (_Artifacts, _FusedPairProducts, false);
    }
  }

  /// <summary>What the first-building variant asks the fold loop to produce alongside the
  /// artifacts: the canonical pairing inline (zero delegates -- the guard-rail rule), or an
  /// erased product writer (one virtual call per node, values and accumulates still hot --
  /// the composed variants' fusion; the separate zip pass re-traversed three arrays and cost
  /// the composed route its win on net8). At most one is set.</summary>
  internal readonly struct ScanBuildRequest<TSource, TAccumulate>
  {
    public ScanBuildRequest(bool fuseCanonicalPairing, ScanProductWriter<TSource, TAccumulate> productWriter)
    {
      FuseCanonicalPairing = fuseCanonicalPairing;
      ProductWriter = productWriter;
    }

    public readonly bool FuseCanonicalPairing;
    public readonly ScanProductWriter<TSource, TAccumulate> ProductWriter;
  }

  /// <summary>The erased product sink: the variant knows TProduct, the pass does not, so the
  /// variant hands over a writer and the fold loop calls it per node. A build that cannot
  /// serve it (the walker fold -- count unknown until the walk ends) simply never fills it,
  /// and the variant zips from the artifacts instead.</summary>
  internal abstract class ScanProductWriter<TSource, TAccumulate>
  {
    /// <summary>Called once, when the build knows the node count, before any Write.</summary>
    public abstract void Initialize(int nodeCount);

    public abstract void Write(int nodeIndex, TSource value, TAccumulate accumulate);

    /// <summary>True once Initialize has run -- the build served this writer.</summary>
    public bool Filled { get; protected set; }
  }

  /// <summary>
  /// A completed fold pass: the value reader (a delegate, so an in-place fold can read the
  /// receiver's own store without copying its values), the accumulations, and the skeleton
  /// -- one immutable int[] deliberately SHARED by every product store zipped from this
  /// pass (subtree sizes are identical across product variants by construction).
  /// </summary>
  internal readonly struct ScanFoldArtifacts<TSource, TAccumulate>
  {
    /// <summary>The array form: a pass that already owns its values as an array hands it
    /// over directly, so zips index it raw -- no delegate per node (the net8 zip rule,
    /// 2026-08-17: the delegate-per-node read cost the composed route its win on the CI
    /// runtime; net10's JIT hid it, net8's did not).</summary>
    public ScanFoldArtifacts(TSource[] values, TAccumulate[] accumulates, int[] subtreeSizes)
    {
      Values = values;
      ValueAt = null;
      Accumulates = accumulates;
      SubtreeSizes = subtreeSizes;
    }

    /// <summary>The reader form: an in-place pass reads the receiver's own store and never
    /// copies its values -- the delegate is the price of not materializing them.</summary>
    public ScanFoldArtifacts(Func<int, TSource> valueAt, TAccumulate[] accumulates, int[] subtreeSizes)
    {
      Values = null;
      ValueAt = valueAt;
      Accumulates = accumulates;
      SubtreeSizes = subtreeSizes;
    }

    public readonly TSource[] Values;
    public readonly Func<int, TSource> ValueAt;
    public readonly TAccumulate[] Accumulates;
    public readonly int[] SubtreeSizes;

    public int Count => Accumulates.Length;
  }
}
