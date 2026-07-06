using BenchmarkDotNet.Attributes;
using Copse;
using Copse.Core;
using Copse.Engine;
using Copse.Generated;
using Copse.Treenumerators;

namespace Copse.Benchmarks
{
  /// <summary>
  /// Prototype A/B: the hand-tuned <see cref="DepthFirstTreenumerator{TValue, TNode, TChildEnumerator}"/>
  /// (baseline) vs the Option-B <see cref="DepthFirstCadenceTreenumerator{TValue, TNode, TChildEnumerator}"/>
  /// over the shared cadence, on identical work. Both drive the same struct child enumerator over the
  /// same tree; the ONLY difference is whether the MoveNext cadence is inlined by hand or funnels
  /// through the cadence's Advance()/Supply state machine. The in-process <c>Ratio</c> is the
  /// hardware-noise-immune answer to "does inverting the seam cost the sync hot path?".
  ///
  /// Category "Cadence" is not one of the five the CI workflow runs, so this stays off the continuous
  /// dashboard; run on demand: <c>dotnet run -c Release -- --anyCategories Cadence</c>.
  /// </summary>
  [MemoryDiagnoser]
  [BenchmarkCategory("Cadence")]
  public class CadenceVsEngineDepthFirst
  {
    [Params(1 << 20)]
    public int N;

    private int[][] _children;
    private int[] _roots;

    [GlobalSetup]
    public void Setup()
    {
      // A complete binary tree over [0, N): node i's children are 2i+1, 2i+2 when in range.
      _children = new int[N][];
      for (int i = 0; i < N; i++)
      {
        int l = 2 * i + 1, r = 2 * i + 2;
        if (r < N) _children[i] = new[] { l, r };
        else if (l < N) _children[i] = new[] { l };
        else _children[i] = System.Array.Empty<int>();
      }
      _roots = new[] { 0 };
    }

    private ArrayChildEnumerator Factory(NodeContext<int> nc) => new ArrayChildEnumerator(_children[nc.Node]);

    [Benchmark(Baseline = true)]
    public long Engine()
    {
      var t = new DepthFirstTreenumerator<int, int, ArrayChildEnumerator>(_roots, Factory, i => i);
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    [Benchmark]
    public long Cadence()
    {
      var t = new DepthFirstCadenceTreenumerator<int, int, ArrayChildEnumerator>(_roots, Factory, i => i);
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    // Direct style (natural inlined control flow) over the shared cross-assembly path -- the codegen
    // twin's shape. Same assembly split as Cadence, so Direct-vs-Cadence isolates the inversion cost.
    [Benchmark]
    public long Direct()
    {
      var t = new DepthFirstDirectTreenumerator<int, int, ArrayChildEnumerator>(_roots, Factory, i => i);
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    // The actual CODEGEN'D sync twin (Copse.CodeGen output), pulling Current-style. Confirms the
    // generated code -- not just the hand-written direct driver -- is at engine parity.
    [Benchmark]
    public long Generated()
    {
      var t = new GeneratedDepthFirstTreenumerator<int, int, ArrayForwardChildEnumerator>(_roots, ForwardFactory, i => i);
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    private ArrayForwardChildEnumerator ForwardFactory(NodeContext<int> nc) => new ArrayForwardChildEnumerator(_children[nc.Node]);

    private struct ArrayForwardChildEnumerator : IForwardChildEnumerator<int>
    {
      private readonly int[] _children;
      private int _i;
      private NodeAndSiblingIndex<int> _current;
      public ArrayForwardChildEnumerator(int[] children) { _children = children; _i = 0; _current = default; }

      public bool MoveNext()
      {
        if (_i < _children.Length)
        {
          _current = new NodeAndSiblingIndex<int>(_children[_i], _i);
          _i++;
          return true;
        }
        return false;
      }

      public NodeAndSiblingIndex<int> Current => _current;

      public void Dispose() { }
    }

    private struct ArrayChildEnumerator : IChildEnumerator<int>
    {
      private readonly int[] _children;
      private int _i;
      public ArrayChildEnumerator(int[] children) { _children = children; _i = 0; }

      public bool MoveNext(out NodeAndSiblingIndex<int> child)
      {
        if (_i < _children.Length)
        {
          child = new NodeAndSiblingIndex<int>(_children[_i], _i);
          _i++;
          return true;
        }
        child = default;
        return false;
      }

      public void Dispose() { }
    }
  }
}
