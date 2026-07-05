using Copse.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Linq.Tests
{
  /// <summary>
  /// Allocation regression tests for the breadth-first <c>Where</c> operator
  /// (<see cref="Copse.Linq.Treenumerators.WhereBreadthFirstTreenumerator{TNode}"/>).
  ///
  /// History / what this guards: the BFT Where implementation once kept a depth-indexed
  /// <c>List&lt;int&gt;</c> (the predicate-skipped-ancestor prefix, <c>_PredSkipPrefix</c>) that
  /// grew to the tree's maximum depth EVEN WHEN NOTHING WAS FILTERED. On a deep degenerate tree
  /// with <c>.Where(_ =&gt; true)</c> (WhereAll -- no node is ever skipped) that list ballooned to
  /// O(depth) (~8.39 MB on a 1M-deep tree) instead of staying O(1). The fix replaced the flat
  /// list with a "tail-carry" prefix that only materializes entries up to the deepest
  /// predicate-skipped ancestor on the current path; with zero skips the stored region stays
  /// empty and the traversal allocates a small, depth-independent constant.
  ///
  /// The bug was purely an ALLOCATION regression -- it never affected the visit stream, whose
  /// correctness is exhaustively covered by Where2InProcessScan. The gh-pages benchmark
  /// (BreadthFirstWhere.DegenerateTree_WhereAll_1M) only *alerts* at 150% with
  /// fail-on-alert: false, so it is not a hard guard. This test is the hard guard.
  ///
  /// Why a RATIO assertion (and not an absolute byte threshold): the regression's signature is
  /// that allocation scales with depth. We therefore measure WhereAll at two depths and assert
  /// that 4x the depth does NOT produce ~4x the allocation. This is robust to runtime/GC/JIT
  /// changes that would shift an absolute byte count, while still catching any return to
  /// O(depth) growth.
  /// </summary>
  [TestClass]
  public class WhereBreadthFirstAllocationTests
  {
    // Two depths a factor of 4 apart. The old O(depth) bug made allocation scale ~linearly
    // with depth, so 4x the depth produced ~4x the bytes. Both depths are large enough that
    // the depth-proportional term (had it existed) would dwarf the fixed per-traversal overhead,
    // yet small enough that the whole test runs in well under a second.
    private const int ShallowDepth = 100_000;
    private const int DeepDepth = 400_000;
    private const int DepthFactor = DeepDepth / ShallowDepth; // 4

    // Allowed growth of alloc(DeepDepth) relative to alloc(ShallowDepth).
    //
    // Fixed code: WhereAll allocation is depth-INDEPENDENT, so the measured ratio is ~1.0
    //   (observed exactly 1.0000, deterministically, across repeated runs -- nothing on the
    //   WhereAll path scales with depth, so the two windows allocate the identical constant).
    // Old buggy code: allocation was O(depth), so the ratio would be ~DepthFactor (~4.0).
    //   (Confirmed live by the WhereNone control below, which is *legitimately* O(depth) and
    //   measures a ratio of ~4.0 -- the exact shape the old WhereAll bug had.)
    //
    // 1.5 sits far below the ~4.0 failure signal and far above the ~1.0 pass signal, so the
    // test has comfortable headroom in both directions and will not flake.
    private const double MaxAllowedGrowthRatio = 1.5;

    private static long MeasureWhereAllBreadthFirst(int depth)
    {
      // Force a clean baseline so unrelated finalizable garbage isn't billed to this window.
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();

      var before = GC.GetAllocatedBytesForCurrentThread();

      Enumerable
        .Range(0, depth)
        .ToDegenerateTree()
        .Where(_ => true) // WhereAll: nothing is ever predicate-skipped -> must be O(1) in depth.
        .Consume(TreeTraversalStrategy.BreadthFirst);

      var after = GC.GetAllocatedBytesForCurrentThread();
      return after - before;
    }

    private static long MeasureWhereNoneBreadthFirst(int depth)
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();

      var before = GC.GetAllocatedBytesForCurrentThread();

      Enumerable
        .Range(0, depth)
        .ToDegenerateTree()
        .Where(_ => false) // WhereNone: every node is predicate-skipped -> legitimately O(depth).
        .Consume(TreeTraversalStrategy.BreadthFirst);

      var after = GC.GetAllocatedBytesForCurrentThread();
      return after - before;
    }

    [TestMethod]
    public void Where_BreadthFirst_WhereAll_OverDeepDegenerateTree_AllocatesIndependentlyOfDepth()
    {
      // Warm up JIT/type init so first-call allocation doesn't pollute the measured windows.
      MeasureWhereAllBreadthFirst(1_000);

      var shallowAllocation = MeasureWhereAllBreadthFirst(ShallowDepth);
      var deepAllocation = MeasureWhereAllBreadthFirst(DeepDepth);

      var growthRatio = (double)deepAllocation / shallowAllocation;

      Console.WriteLine(
        $"WhereAll BFT allocation: depth {ShallowDepth} = {shallowAllocation} bytes, " +
        $"depth {DeepDepth} = {deepAllocation} bytes, growth ratio = {growthRatio:F3} " +
        $"(depth factor = {DepthFactor}, max allowed ratio = {MaxAllowedGrowthRatio}).");

      Assert.IsTrue(
        growthRatio < MaxAllowedGrowthRatio,
        $"BFT WhereAll allocation scaled with depth: {DeepDepth} allocated {deepAllocation} bytes " +
        $"vs {shallowAllocation} bytes at depth {ShallowDepth} (growth ratio {growthRatio:F3}). " +
        $"Expected the ratio to stay below {MaxAllowedGrowthRatio} (allocation should be ~O(1) in depth). " +
        $"This is the WhereBreadthFirstTreenumerator depth-indexed-prefix (_PredSkipPrefix) memory " +
        $"regression -- a depth factor of {DepthFactor} should NOT produce a ~{DepthFactor}x allocation.");
    }

    [TestMethod]
    public void Where_BreadthFirst_WhereNone_OverDeepDegenerateTree_LegitimatelyScalesWithDepth()
    {
      // Sanity / contrast control: WhereNone *should* allocate O(depth) (every node is skipped,
      // so the skipped-ancestor prefix is legitimately as deep as the tree). This both documents
      // the boundary of the O(1) guarantee (it applies to WhereAll, NOT WhereNone) and proves the
      // measurement apparatus can actually SEE depth-proportional growth -- i.e. that the WhereAll
      // assertion above is testing something real. WhereNone's ~DepthFactor ratio is the same shape
      // the old WhereAll bug had.
      MeasureWhereNoneBreadthFirst(1_000);

      var shallowAllocation = MeasureWhereNoneBreadthFirst(ShallowDepth);
      var deepAllocation = MeasureWhereNoneBreadthFirst(DeepDepth);

      var growthRatio = (double)deepAllocation / shallowAllocation;

      Console.WriteLine(
        $"WhereNone BFT allocation: depth {ShallowDepth} = {shallowAllocation} bytes, " +
        $"depth {DeepDepth} = {deepAllocation} bytes, growth ratio = {growthRatio:F3} " +
        $"(depth factor = {DepthFactor}).");

      // Expect roughly linear growth (~DepthFactor). Use a wide band so this control is itself
      // not flaky: anything clearly above the WhereAll ceiling confirms depth-proportional growth.
      Assert.IsTrue(
        growthRatio > 2.0,
        $"Expected WhereNone to allocate O(depth) (growth ratio near {DepthFactor}), but measured " +
        $"{growthRatio:F3} ({deepAllocation} vs {shallowAllocation} bytes). If this dropped to ~1, the " +
        $"allocation measurement is no longer observing depth-proportional growth and the WhereAll " +
        $"regression guard may be vacuous.");
    }
  }
}
