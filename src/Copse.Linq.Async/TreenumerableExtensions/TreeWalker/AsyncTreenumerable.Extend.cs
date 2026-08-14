using Copse.Async;
using Copse.Linq.Async.Treenumerables;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Co-bind on the carrier: relabel the whole terrain by an observation of every focus,
    /// and keep standing where you are. The observer receives a walker, so it can extract,
    /// step, and extend -- anything a vantage affords. An extension rather than a struct
    /// member by the carriers-in-Core/algebras-in-Linq split: the result is a walkable,
    /// walkables stream, and streaming needs the Walk adapter -- which lives here, not in
    /// Core (the same reason <c>ITreenumerable</c> carries no <c>Select</c>).
    /// </summary>
    public static AsyncTreeWalker<TResult, THandle> Extend<TValue, THandle, TResult>(
      this AsyncTreeWalker<TValue, THandle> walker,
      Func<AsyncTreeWalker<TValue, THandle>, ValueTask<TResult>> observer)
      => new AsyncTreeWalker<TResult, THandle>(
        walker.Walkable.Extend<TValue, THandle, TResult>(
          (source, handle) => observer(new AsyncTreeWalker<TValue, THandle>(source, handle))),
        walker.Focus);
  }
}
