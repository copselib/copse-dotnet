using Copse.Benchmarks.Trees;
using Copse.Core;
using Copse.Linq;
using Copse.Trees;
using System.Linq;

namespace Copse.Benchmarks
{
  /// <summary>
  /// The canonical benchmark workloads: every tree shape at standardized size tiers, defined in
  /// exactly one place so cross-benchmark comparisons are same-scale by construction.
  ///
  /// <para><b>Why sizes are standardized (the noise floor).</b> Sub-millisecond benchmarks proved
  /// noisy on both local machines and shared CI runners, so historically each benchmark's node
  /// count was bumped until repeated local runs stopped swinging -- which fixed the noise but
  /// left nearly every benchmark at a different, undocumented size. The standardization keeps the
  /// noise constraint as an invariant -- every row must clear ~1 ms (the observed noise
  /// threshold) on the SLOWEST runner CPU seen, with ~10 ms as the design target -- while making
  /// every operator and shape directly comparable. Measured at the Mega tier (first run,
  /// 2026-07-09): the cheapest rows are the trivial-forest drains at ~2.6 ns/node (~3 ms; the
  /// forest has no child pulls at all), most rows land 10-300 ms, and the heaviest (Mega x Mega
  /// Union) near a second: all above the threshold, none wasteful. If forest rows ever prove
  /// noisy in practice, revisit -- do NOT bump only the forest parameter; that would break
  /// cross-shape comparability.</para>
  ///
  /// <para><b>Why tiers quantize per shape.</b> Shapes cannot hit an exact shared count: binary
  /// trees grow as 2^d - 1, the triangle as d(d+1)/2. Each tier names a power-of-two TARGET and
  /// each shape takes the closest achievable parameter -- all within +-0.2%, three orders of
  /// magnitude below run-to-run noise, so the counts are comparable in every practical sense.</para>
  ///
  /// <para><b>Mega tier (target 2^20 = 1,048,576):</b></para>
  /// <list type="bullet">
  ///   <item>Chain / Forest: Range(0, 1 &lt;&lt; 20) -> exactly 1,048,576</item>
  ///   <item>Binary: complete binary tree pruned at depth 20 -> 2^20 - 1 = 1,048,575</item>
  ///   <item>Triangle: pruned at depth 1448 -> 1449*1450/2 = 1,050,525 (+0.19%; 1448 is the
  ///     closest triangular number to 2^20)</item>
  ///   <item>DeepChains: DeepTree(20) -> 2^20 - 1 = 1,048,575 (a forest of 20 chains with
  ///     geometrically increasing lengths; the deepest is 2^19 nodes -- the deep-path stressor)</item>
  /// </list>
  ///
  /// <para><b>Stress tier (target 2^22 = 4,194,304)</b> exists ONLY for the engine-traversal
  /// family, where scaling itself is the question; operator benchmarks stay at Mega:</para>
  /// <list type="bullet">
  ///   <item>Chain / Forest: Range(0, 1 &lt;&lt; 22) -> exactly 4,194,304</item>
  ///   <item>Binary: pruned at depth 22 -> 4,194,303</item>
  ///   <item>Triangle: pruned at depth 2896 -> 2897*2898/2 = 4,197,753 (+0.08%)</item>
  /// </list>
  ///
  /// <para><b>Documented exceptions</b> are allowed where a tier violates a physical budget (for
  /// example serializer deep-tree rows, where a Mega-tier degenerate serialization is a ~10 MB
  /// string per op) -- but the exception and its reason must be stated at the benchmark.</para>
  ///
  /// <para><b>Reading results.</b> Shared CI runners are a CPU lottery (EPYC 9V74 / EPYC 7763 /
  /// Xeon 8573C observed, spanning roughly +-30%), and each matrix leg draws its own machine.
  /// Same-run comparisons (the AsyncOverhead ratio pairs, rows within one leg) are trustworthy;
  /// cross-run absolute deltas are not, until checked against HostEnvironmentInfo.ProcessorName
  /// in the run artifacts.</para>
  /// </summary>
  internal static class CanonicalTrees
  {
    // Exact node counts per tier, for benchmark naming and validation.
    public const int MegaChain = 1 << 20;              // 1,048,576
    public const int MegaBinary = (1 << 20) - 1;       // 1,048,575 (depth 20)
    public const int MegaTriangle = 1449 * 1450 / 2;   // 1,050,525 (depth 1448)
    public const int MegaDeepChains = (1 << 20) - 1;   // 1,048,575 (DeepTree(20))

    public const int StressChain = 1 << 22;            // 4,194,304
    public const int StressBinary = (1 << 22) - 1;     // 4,194,303 (depth 22)
    public const int StressTriangle = 2897 * 2898 / 2; // 4,197,753 (depth 2896)

    private const int MegaBinaryDepth = 20;
    private const int MegaTriangleDepth = 1448;
    private const int MegaDeepChainsWidth = 20;

    private const int StressBinaryDepth = 22;
    private const int StressTriangleDepth = 2896;

    // ----- Mega tier (the default for every operator benchmark) -----

    /// <summary>A single degenerate chain: maximum depth, one node per level.</summary>
    public static ITreenumerable<int> MegaChainTree()
      => Enumerable.Range(0, MegaChain).ToDegenerateTree();

    /// <summary>A trivial forest: maximum breadth, every node a root.</summary>
    public static ITreenumerable<int> MegaForest()
      => Enumerable.Range(0, MegaChain).ToTrivialForest();

    /// <summary>The complete binary tree: balanced branching, log-depth.</summary>
    public static ITreenumerable<int> MegaBinaryTree()
      => new CompleteBinaryTree()
        .PruneBefore(nodeContext => nodeContext.Position.Depth == MegaBinaryDepth);

    /// <summary>The triangle tree: level width grows linearly with depth.</summary>
    public static ITreenumerable<int> MegaTriangleTree()
      => new TriangleTree()
        .PruneAfter(nodeContext => nodeContext.Position.Depth == MegaTriangleDepth);

    /// <summary>Twenty chains of geometrically increasing length; the deep-path stressor.</summary>
    public static ITreenumerable<int> MegaDeepChainsTree()
      => new DeepTree(MegaDeepChainsWidth);

    // ----- Stress tier (engine-traversal scaling rows only) -----

    public static ITreenumerable<int> StressChainTree()
      => Enumerable.Range(0, StressChain).ToDegenerateTree();

    public static ITreenumerable<int> StressForest()
      => Enumerable.Range(0, StressChain).ToTrivialForest();

    public static ITreenumerable<int> StressBinaryTree()
      => new CompleteBinaryTree()
        .PruneBefore(nodeContext => nodeContext.Position.Depth == StressBinaryDepth);

    public static ITreenumerable<int> StressTriangleTree()
      => new TriangleTree()
        .PruneAfter(nodeContext => nodeContext.Position.Depth == StressTriangleDepth);
  }
}
