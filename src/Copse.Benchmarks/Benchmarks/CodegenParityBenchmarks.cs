using BenchmarkDotNet.Attributes;
using Copse.Core;
using Copse.Linq.Generated;  // Where twins
using Copse.Linq.Treenumerators; // WhereDepthFirstTreenumerator / WhereBreadthFirstTreenumerator (internal, via IVT)
using Copse.Traversal;
using Copse.Treenumerators;
using System;

namespace Copse.Benchmarks
{
  // The codegen ADOPTION gate for the Where wrappers: does the generated sync twin match the
  // hand-tuned sync operator's perf? If yes, the twins can REPLACE the hand-written operators (the
  // DRY payoff). Same-run baseline ratios (hardware-noise-immune). Category "Parity" -> off the
  // continuous dashboard; run: dotnet run -c Release -- --anyCategories Parity
  //
  // (The engines are already adopted -- the generated twin IS DepthFirst/BreadthFirstTreenumerator
  // now, so there is nothing left to A/B there. These isolate the two remaining hand-tuned operators.)

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

  internal struct ParityChildCursor : IChildCursor<int>
  {
    private readonly int[] _children;
    private int _i;
    public ParityChildCursor(int[] children) { _children = children; _i = 0; }
    public ChildResult<int> MoveNext()
    {
      if (_i < _children.Length) { var r = new ChildResult<int>(new NodeAndSiblingIndex<int>(_children[_i], _i)); _i++; return r; }
      return default;
    }
    public void Dispose() { }
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
      => () => new DepthFirstTreenumerator<int, int, ParityChildCursor>(_roots, nc => new ParityChildCursor(_children[nc.Node]), i => i);

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
      => () => new BreadthFirstTreenumerator<int, int, ParityChildCursor>(_roots, nc => new ParityChildCursor(_children[nc.Node]), i => i);

    [Benchmark(Baseline = true)]
    public long HandTuned()
      => ParityTree.Drain(new WhereBreadthFirstTreenumerator<int>(Inner, ParityTree.Keep, NodeTraversalStrategies.SkipNode));

    [Benchmark]
    public long Generated()
      => ParityTree.Drain(new GeneratedWhereBreadthFirstTreenumerator<int>(Inner, ParityTree.Keep, NodeTraversalStrategies.SkipNode));
  }
}
