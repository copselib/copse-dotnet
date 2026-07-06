using BenchmarkDotNet.Attributes;
using Copse;
using Copse.Core;
using Copse.Generated;       // engine twins (GeneratedBreadthFirstTreenumerator)
using Copse.Linq.Generated;  // Where twins
using Copse.Linq.Treenumerators; // WhereDepthFirstTreenumerator / WhereBreadthFirstTreenumerator (internal, via IVT)
using Copse.Traversal;
using Copse.Treenumerators;
using System;

namespace Copse.Benchmarks
{
  // The codegen ADOPTION gate: does the generated sync twin match the hand-tuned sync code's perf?
  // If yes, the twins can REPLACE the hand-written engines/operators (the DRY payoff). Same-run
  // baseline ratios (hardware-noise-immune). Category "Parity" -> off the continuous dashboard;
  // run: dotnet run -c Release -- --anyCategories Parity
  //
  // (The DFS engine is already covered by DepthFirstDriverBenchmarks at ~1.0x; these add the untested
  // comparisons: the BFS engine -- whose async port RESTRUCTURED the seams -- and both Where wrappers.)

  internal static class ParityTree
  {
    public static int[][] Build(int n)
    {
      var children = new int[n][];
      for (int i = 0; i < n; i++)
      {
        int l = 2 * i + 1, r = 2 * i + 2;
        if (r < n) children[i] = new[] { l, r };
        else if (l < n) children[i] = new[] { l };
        else children[i] = Array.Empty<int>();
      }
      return children;
    }

    public static long Drain(ITreenumerator<int> t)
    {
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    // Drop multiples of 3 -- exercises child promotion / depth compression in Where.
    public static readonly Func<NodeContext<int>, bool> Keep = nc => nc.Node % 3 != 0;
  }

  internal struct ParityChildEnumerator : IChildEnumerator<int>
  {
    private readonly int[] _children;
    private int _i;
    public ParityChildEnumerator(int[] children) { _children = children; _i = 0; }
    public bool MoveNext(out NodeAndSiblingIndex<int> child)
    {
      if (_i < _children.Length) { child = new NodeAndSiblingIndex<int>(_children[_i], _i); _i++; return true; }
      child = default; return false;
    }
    public void Dispose() { }
  }

  internal struct ParityForwardChildEnumerator : IForwardChildEnumerator<int>
  {
    private readonly int[] _children;
    private int _i;
    private NodeAndSiblingIndex<int> _current;
    public ParityForwardChildEnumerator(int[] children) { _children = children; _i = 0; _current = default; }
    public bool MoveNext()
    {
      if (_i < _children.Length) { _current = new NodeAndSiblingIndex<int>(_children[_i], _i); _i++; return true; }
      return false;
    }
    public NodeAndSiblingIndex<int> Current => _current;
    public void Dispose() { }
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("Parity")]
  public class BfsEngineParity
  {
    [Params(1 << 18)] public int N;
    private int[][] _children;
    private int[] _roots;

    [GlobalSetup] public void Setup() { _children = ParityTree.Build(N); _roots = new[] { 0 }; }

    private ParityChildEnumerator OutFactory(NodeContext<int> nc) => new(_children[nc.Node]);
    private ParityForwardChildEnumerator FwdFactory(NodeContext<int> nc) => new(_children[nc.Node]);

    [Benchmark(Baseline = true)]
    public long HandTuned()
      => ParityTree.Drain(new BreadthFirstTreenumerator<int, int, ParityChildEnumerator>(_roots, OutFactory, i => i));

    [Benchmark]
    public long Generated()
      => ParityTree.Drain(new GeneratedBreadthFirstTreenumerator<int, int, ParityForwardChildEnumerator>(_roots, FwdFactory, i => i));
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("Parity")]
  public class DftWhereParity
  {
    [Params(1 << 18)] public int N;
    private int[][] _children;
    private int[] _roots;

    [GlobalSetup] public void Setup() { _children = ParityTree.Build(N); _roots = new[] { 0 }; }

    // Same inner engine for both, so the ratio isolates the Where wrapper cost.
    private Func<ITreenumerator<int>> Inner
      => () => new DepthFirstTreenumerator<int, int, ParityChildEnumerator>(_roots, nc => new ParityChildEnumerator(_children[nc.Node]), i => i);

    [Benchmark(Baseline = true)]
    public long HandTuned()
      => ParityTree.Drain(new WhereDepthFirstTreenumerator<int>(Inner, ParityTree.Keep, NodeTraversalStrategies.SkipNode));

    [Benchmark]
    public long Generated()
      => ParityTree.Drain(new GeneratedWhereDepthFirstTreenumerator<int>(Inner, ParityTree.Keep, NodeTraversalStrategies.SkipNode));
  }

  [MemoryDiagnoser]
  [BenchmarkCategory("Parity")]
  public class BftWhereParity
  {
    [Params(1 << 18)] public int N;
    private int[][] _children;
    private int[] _roots;

    [GlobalSetup] public void Setup() { _children = ParityTree.Build(N); _roots = new[] { 0 }; }

    private Func<ITreenumerator<int>> Inner
      => () => new BreadthFirstTreenumerator<int, int, ParityChildEnumerator>(_roots, nc => new ParityChildEnumerator(_children[nc.Node]), i => i);

    [Benchmark(Baseline = true)]
    public long HandTuned()
      => ParityTree.Drain(new WhereBreadthFirstTreenumerator<int>(Inner, ParityTree.Keep, NodeTraversalStrategies.SkipNode));

    [Benchmark]
    public long Generated()
      => ParityTree.Drain(new GeneratedWhereBreadthFirstTreenumerator<int>(Inner, ParityTree.Keep, NodeTraversalStrategies.SkipNode));
  }
}
