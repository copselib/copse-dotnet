using Copse;
using Copse.Core;
using Copse.Linq;
using Copse.Linq.Treenumerables;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // DIAGNOSTIC CLASS -- BRANCH ONLY, NEVER MERGES TO MAIN. Isolates the EPYC-only ~4x
  // regression the sentinel-completion arc introduced on the warm walker probe sweep
  // (BufferProbes.Walk_over_MaterializedPreorder: EPYC 7763 went 10.2 -> 42.3 ms at the arc
  // while Xeon draws sat flat -- see walker-design ledger, EPYC PROBE REGRESSION).
  //
  // All four rows traverse the same materialized preorder capture through the SAME public
  // topology probes; they differ only in the step-plumbing shape between the probes. Same
  // process, same machine, so the row ratios are trustworthy on whatever model the run
  // draws; a dispatch that lands on EPYC names the culprit.
  //
  //  - Walker           : the shipping TreeWalker sweep (the regressed shape) -- baseline.
  //  - TopologyDirect   : bare probes, no walker steps at all -- the engines' own floor;
  //                       splits "walker layer" from "topology engines".
  //  - ResultCarriesWalker : step result CARRIES the constructed walker + a bool, instead of
  //                       reconstructing it through the StepOutcome switch at every consume.
  //  - OptionOfWalker   : the pre-arc shape (Option over the walker), the historical control
  //                       that ran 10.2 ms on the same EPYC model.
  [MemoryDiagnoser]
  [BenchmarkCategory("Buffer")]
  public class WalkerStepShape
  {
    private ITreenumerableBuffer<int> _Capture;
    private ITreeTopology<int, int> _Topology;

    [GlobalSetup]
    public void Setup()
    {
      _Capture = CanonicalTrees.MegaTriangleTree().Materialize(BufferLayout.Preorder);
      _Capture.Consume(TreeTraversalStrategy.DepthFirst);
      _Topology = _Capture.GetTreeWalker().Topology;

      // Warm every axis so all rows measure steady-state probe reads.
      Walker();
      TopologyDirect();
    }

    // ----- Row 1: the shipping shape -----

    [Benchmark(Baseline = true)]
    public long Walker()
    {
      var door = _Capture.GetTreeWalker();
      var checksum = 0L;

      for (var rootIndex = 0; ; rootIndex++)
      {
        var root = door.MoveToRoot(rootIndex);
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
      }

      return checksum;
    }

    // ----- Row 2: bare probes, no walker layer -----

    [Benchmark]
    public long TopologyDirect()
    {
      var checksum = 0L;

      for (var rootIndex = 0; ; rootIndex++)
      {
        var root = _Topology.TryGetRootAt(rootIndex);
        if (!root.HasValue)
          break;
        checksum += WalkSubtreeDirect(root.Value.Handle);
      }

      return checksum;
    }

    private long WalkSubtreeDirect(int handle)
    {
      var checksum = (long)_Topology.GetValue(handle);

      for (var childIndex = 0; ; childIndex++)
      {
        var child = _Topology.TryGetChildAt(handle, childIndex);
        if (!child.HasValue)
          break;
        checksum += WalkSubtreeDirect(child.Value.Handle);
      }

      return checksum;
    }

    // ----- Row 3: the step result carries the walker -----

    private readonly struct CarriedResult
    {
      public CarriedResult(TreeWalker<int, int> walker)
      {
        Walker = walker;
        HasValue = true;
      }

      public readonly TreeWalker<int, int> Walker;
      public readonly bool HasValue;
    }

    private CarriedResult StepToChild(in TreeWalker<int, int> walker, int childIndex)
    {
      var child = _Topology.TryGetChildAt(walker.Focus, childIndex);
      return child.HasValue ? new CarriedResult(walker.At(child.Value.Handle)) : default;
    }

    [Benchmark]
    public long ResultCarriesWalker()
    {
      var checksum = 0L;

      for (var rootIndex = 0; ; rootIndex++)
      {
        var root = _Topology.TryGetRootAt(rootIndex);
        if (!root.HasValue)
          break;
        checksum += WalkSubtreeCarried(new TreeWalker<int, int>(_Topology, root.Value.Handle));
      }

      return checksum;
    }

    private long WalkSubtreeCarried(TreeWalker<int, int> walker)
    {
      var checksum = (long)walker.GetValue();

      for (var childIndex = 0; ; childIndex++)
      {
        var child = StepToChild(in walker, childIndex);
        if (!child.HasValue)
          break;
        checksum += WalkSubtreeCarried(child.Walker);
      }

      return checksum;
    }

    // ----- Row 4: the pre-arc shape, Option over the walker -----

    private Option<TreeWalker<int, int>> OptionStepToChild(in TreeWalker<int, int> walker, int childIndex)
    {
      var child = _Topology.TryGetChildAt(walker.Focus, childIndex);
      return child.HasValue
        ? new Option<TreeWalker<int, int>>(walker.At(child.Value.Handle))
        : default;
    }

    [Benchmark]
    public long OptionOfWalker()
    {
      var checksum = 0L;

      for (var rootIndex = 0; ; rootIndex++)
      {
        var root = _Topology.TryGetRootAt(rootIndex);
        if (!root.HasValue)
          break;
        checksum += WalkSubtreeOption(new TreeWalker<int, int>(_Topology, root.Value.Handle));
      }

      return checksum;
    }

    private long WalkSubtreeOption(TreeWalker<int, int> walker)
    {
      var checksum = (long)walker.GetValue();

      for (var childIndex = 0; ; childIndex++)
      {
        var child = OptionStepToChild(in walker, childIndex);
        if (!child.HasValue)
          break;
        checksum += WalkSubtreeOption(child.Value);
      }

      return checksum;
    }
  }
}
