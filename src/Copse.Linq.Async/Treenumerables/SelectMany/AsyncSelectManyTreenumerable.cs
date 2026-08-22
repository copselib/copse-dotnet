using Copse.Async;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The composite-width bind: the depth-first machine over the source with a struct
  // expansion selector -- the user's selector alone, or a collapsed chain's arrow folded
  // ahead of it through the left door (SELECTMANY_DESIGN.md Addendum V). Breadth-first is
  // a DOCUMENTED CAPTURE: each acquisition captures the depth-first result and replays it
  // (the streaming route is recorded in Addendum IV; deferred on demand).
  internal sealed class AsyncSelectManyTreenumerable<TSource, TResult, TExpansionSelector> : IAsyncTreenumerable<TResult>
    where TExpansionSelector : struct, IAsyncExpansionSelector<TSource, TResult>
  {
    public AsyncSelectManyTreenumerable(
      Func<IAsyncTreenumerator<TSource>> sourceDepthFirstTreenumeratorFactory,
      TExpansionSelector expansionSelector)
    {
      _SourceDepthFirstTreenumeratorFactory = sourceDepthFirstTreenumeratorFactory;
      _ExpansionSelector = expansionSelector;
    }

    private readonly Func<IAsyncTreenumerator<TSource>> _SourceDepthFirstTreenumeratorFactory;
    private readonly TExpansionSelector _ExpansionSelector;

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator()
      => new AsyncSelectManyDepthFirstTreenumerator<TSource, TResult, TExpansionSelector>(
        _SourceDepthFirstTreenumeratorFactory,
        _ExpansionSelector);

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator()
      => AsyncTree.CreateDepthFirst(GetAsyncDepthFirstTreenumerator).Materialize().GetAsyncBreadthFirstTreenumerator();
  }
}
