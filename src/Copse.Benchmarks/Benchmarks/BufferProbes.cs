using Copse;
using Copse.Core;
using Copse.Linq;
using Copse.Linq.Treenumerables;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // BUFFER PROBES -- the walker probe surface over captures: TryGetChildAt / TryGetParent /
  // TryGetRootAt driven through TreeWalker sweeps. Seeded 2026-08-16 as the instrument for
  // the adjacency-engine work: until this family, the probe/topology layer had ZERO rows --
  // every existing Buffer row (Materialize, Replay, FlatDecode) rides the visit-stream
  // decoders, which never consult the adjacency engines.
  //
  // Coverage claim (the BENCHMARKING.md #4 expiry clause): these rows reach the internal
  // adjacency engines (PreorderAdjacencyIndex / LevelOrderAdjacencyIndex) through the
  // buffer's probe surface -- the walker routes every move through the buffer's topology.
  // If walker probes ever gain a path that bypasses the topology engines, these rows no
  // longer cover them and this family needs revisiting.
  //
  // Two temperatures, deliberately:
  //  - Walk_over_Materialized* are WARM: the shared capture's index is fully scanned during
  //    setup, so the rows measure steady-state probe reads -- what a consumer pays per walk.
  //  - Walk_over_Memoized* are COLD: a fresh Memoize() per invocation, FED FROM the settled
  //    capture (feed cost is same-run measurable -- it is the MaterializeReplay rows), so
  //    every invocation drives the growing engine's scan through grow-precedes-read. The
  //    scan machinery's time AND allocation land here, every invocation.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer", "BufferProbes")]
  public class BufferProbes
  {
    private ITreenumerableBuffer<int> _PreorderCapture;
    private ITreenumerableBuffer<int> _LevelOrderCapture;

    [GlobalSetup]
    public void Setup()
    {
      _PreorderCapture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.Preorder);
      _LevelOrderCapture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.LevelOrder);

      // Materialize is deferred: settle the captures, then drive each capture's adjacency
      // scan to completion with one full sweep so the warm rows measure probe READS only.
      _PreorderCapture.Consume(TreeTraversalStrategy.DepthFirst);
      _LevelOrderCapture.Consume(TreeTraversalStrategy.BreadthFirst);
      WalkSweep(_PreorderCapture);
      WalkSweep(_LevelOrderCapture);
    }

    [Benchmark]
    public long Walk_over_MaterializedPreorder()
      => WalkSweep(_PreorderCapture);

    [Benchmark]
    public long Walk_over_MaterializedLevelOrder()
      => WalkSweep(_LevelOrderCapture);

    [Benchmark]
    public long Walk_over_MemoizedPreorder()
    {
      using (var memo = ((IDepthFirstTreenumerable<int>)_PreorderCapture).Memoize())
        return WalkSweep(memo);
    }

    [Benchmark]
    public long Walk_over_MemoizedLevelOrder()
    {
      using (var memo = ((IBreadthFirstTreenumerable<int>)_LevelOrderCapture).Memoize())
        return WalkSweep(memo);
    }

    // The guard rail: the receiver-smart in-place fold grabs the adjacency engine's raw
    // store (the bulk-fold seam) and bypasses per-probe dispatch entirely. This row must
    // NOT move when the probe machinery changes underneath it.
    [Benchmark]
    public void LeaffixScan_over_MaterializedPreorder()
      => _PreorderCapture
        .LeaffixScan(leaf => (long)leaf, (left, right) => left + right, (accumulate, node) => accumulate + node)
        .Consume(TreeTraversalStrategy.DepthFirst);

    // A full descend/ascend sweep: every node reached via TryGetChildAt (sequential child
    // ordinals -- the dominant walker access pattern), every non-root re-probed via
    // TryGetParent on the way back up, roots enumerated via TryGetRootAt. The checksum
    // keeps the probes observable; the sweep itself allocates nothing, so the Alloc column
    // is the adjacency engine's own story.
    private static long WalkSweep(IWalkableTreenumerable<int, int> walkable)
    {
      var door = walkable.TryGetTreeWalker();

      if (!door.HasValue)
        return 0;

      var checksum = 0L;

      for (var rootIndex = 0; ; rootIndex++)
      {
        var root = door.Value.MoveToRoot(rootIndex);

        if (!root.HasValue)
          break;

        checksum += WalkSubtree(root.Value);
      }

      return checksum;
    }

    private static long WalkSubtree(TreeWalker<int, int> walker)
    {
      var checksum = (long)walker.GetValue();

      for (var childIndex = 0; ; childIndex++)
      {
        var child = walker.MoveToChild(childIndex);

        if (!child.HasValue)
          break;

        checksum += WalkSubtree(child.Value);
        checksum += child.Value.MoveToParent().Value.GetValue();
      }

      return checksum;
    }
  }
}
