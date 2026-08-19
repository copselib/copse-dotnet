using Copse.Core;
using Copse.Core.Async;

namespace Copse.Benchmarks
{
  // THE MEASUREMENT BOUNDARY (design-docs/BENCHMARKING.md, "What the canonical trees actually
  // measure"). Benchmark trees are built with operators -- the bounded shapes need a positional
  // prune -- and an operator in the scaffolding is a COMPOSITION CITIZEN: a row's first operator
  // joins with it into one machine, so the row measures one operator more than its name says and
  // any change to composition machinery moves the whole suite at once (observed 2026-08-19).
  //
  // Isolation is a property of the TREENUMERABLE, not the treenumerator: a wrapper that claims no
  // composition doors cannot be composed into, and construction can never join the algebra under
  // test. That is all a benchmark needs, so this barrier forwards acquisition and NOTHING ELSE --
  // no treenumerator layer, no per-pull cost, no dilution of the numbers.
  //
  // This is deliberately NOT Copse.Linq's Hide. Hide also wraps the treenumerator (it hides the
  // concrete type too, which is its job and a real per-pull cost). Paying that on every row would
  // shrink every measured win by the fraction of the row the wrapper occupies -- worst exactly
  // where the margins are thinnest, on the cheap shapes that measure little more than the engine.
  internal sealed class IsolatedTreenumerable<TNode> : ITreenumerable<TNode>
  {
    public IsolatedTreenumerable(ITreenumerable<TNode> source) => _Source = source;

    private readonly ITreenumerable<TNode> _Source;

    public ITreenumerator<TNode> GetDepthFirstTreenumerator() => _Source.GetDepthFirstTreenumerator();
    public ITreenumerator<TNode> GetBreadthFirstTreenumerator() => _Source.GetBreadthFirstTreenumerator();
  }

  internal sealed class IsolatedAsyncTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    public IsolatedAsyncTreenumerable(IAsyncTreenumerable<TNode> source) => _Source = source;

    private readonly IAsyncTreenumerable<TNode> _Source;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _Source.GetAsyncDepthFirstTreenumerator();
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _Source.GetAsyncBreadthFirstTreenumerator();
  }

  internal static class MeasurementBoundary
  {
    /// <summary>Hands the tree out with no composition doors, at no per-pull cost.</summary>
    public static ITreenumerable<TNode> Isolate<TNode>(this ITreenumerable<TNode> source)
      => new IsolatedTreenumerable<TNode>(source);

    public static IAsyncTreenumerable<TNode> Isolate<TNode>(this IAsyncTreenumerable<TNode> source)
      => new IsolatedAsyncTreenumerable<TNode>(source);
  }
}
