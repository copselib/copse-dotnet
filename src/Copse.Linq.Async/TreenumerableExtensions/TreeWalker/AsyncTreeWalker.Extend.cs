using Copse.Async;
using Copse.Linq.Async.Topologies;
using Copse.Linq.Async.Treenumerables;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreeWalker
  {
    /// <summary>
    /// Co-bind on the carrier: relabel the whole topology by an observation of every focus,
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
        new AsyncExtendWalkable<TValue, THandle, TResult>(
          new AsyncWalkerTopology<TValue, THandle>(walker),
          // The observer labels through the ORIGINAL walker, not the reconstituted topology:
          // labels are At-stances on the source, so the counit's struct identity
          // (Duplicate().GetValue() == the walker itself) survives the re-plumb -- the
          // wrapper serves adjacency and streaming, where identity never matters.
          (boundTopology, handle) => observer(walker.At(handle))),
        walker.Focus);
  }
}
