using BenchmarkDotNet.Attributes;
using Copse;
using Copse.Core;
using Copse.Traversal;
using Copse.Generated;
using Copse.Treenumerators;

namespace Copse.Benchmarks
{
  /// <summary>
  /// A/B of the DFT sync drivers, on identical work (same tree, same struct child enumerator):
  /// the hand-tuned <see cref="DepthFirstTreenumerator{TValue, TNode, TChildEnumerator}"/> (baseline)
  /// vs the direct-style <see cref="DepthFirstDirectTreenumerator{TValue, TNode, TChildEnumerator}"/>
  /// over the shared cross-assembly <c>DepthFirstPathState</c>, vs the actual codegen'd
  /// <see cref="GeneratedDepthFirstTreenumerator{TValue, TNode, TChildEnumerator}"/>. Confirms the
  /// generated sync twin is at engine parity. The in-process <c>Ratio</c> is hardware-noise-immune.
  ///
  /// Category "Drivers" is not one of the five the CI workflow runs, so this stays off the continuous
  /// dashboard; run on demand: <c>dotnet run -c Release -- --anyCategories Drivers</c>.
  /// </summary>
  [MemoryDiagnoser]
  [BenchmarkCategory("Drivers")]
  public class DepthFirstDriverBenchmarks
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

    // Hand-written direct style over the shared cross-assembly path -- the codegen twin's shape.
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

    // The actual CODEGEN'D sync twin (Copse.CodeGen output), now pulling struct-return via IChildCursor.
    // Confirms the generated code -- not just the hand-written cursor driver -- is at engine parity.
    [Benchmark]
    public long Generated()
    {
      var t = new GeneratedDepthFirstTreenumerator<int, int, ArrayChildCursor>(_roots, CursorFactory, i => i);
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    // The candidate: by-value struct-return pull (IChildCursor) over the SAME shared path as Direct.
    // Direct-vs-Cursor isolates return-by-value vs out. Stores nothing -> no frame bloat (unlike
    // Generated's Current-style), and it's the shape that would also work in async.
    [Benchmark]
    public long Cursor()
    {
      var t = new DepthFirstCursorTreenumerator<int, int, ArrayChildCursor>(_roots, CursorFactory, i => i);
      long sum = 0;
      while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
        sum += t.VisitCount;
      t.Dispose();
      return sum;
    }

    private ArrayChildCursor CursorFactory(NodeContext<int> nc) => new ArrayChildCursor(_children[nc.Node]);

    private struct ArrayChildCursor : IChildCursor<int>
    {
      private readonly int[] _children;
      private int _i;
      public ArrayChildCursor(int[] children) { _children = children; _i = 0; }

      public ChildResult<int> MoveNext()
      {
        if (_i < _children.Length)
        {
          var r = new ChildResult<int>(new NodeAndSiblingIndex<int>(_children[_i], _i));
          _i++;
          return r;
        }
        return default;
      }

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
