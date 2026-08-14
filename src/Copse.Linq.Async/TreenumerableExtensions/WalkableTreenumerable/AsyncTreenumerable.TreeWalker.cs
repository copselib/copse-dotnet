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

    /// <summary>Duplicate: the tree of walkers, still standing at this focus -- extend of
    /// the identity, which is the definition. Duplicating and extracting recovers the
    /// walker: the counit, readable in the types.</summary>
    public static AsyncTreeWalker<AsyncTreeWalker<TValue, THandle>, THandle> Duplicate<TValue, THandle>(
      this AsyncTreeWalker<TValue, THandle> walker)
      => walker.Extend(focus => new ValueTask<AsyncTreeWalker<TValue, THandle>>(focus));

    /// <summary>The reverse door: the treenumerable this stance denotes -- the subtree
    /// rooted at the focus, as a severed re-rooted view sharing the source's handles.
    /// Identical to the label <c>Subtrees()</c> stamps at this focus (pinned). The round
    /// trip tree → root walker → <c>Subtree()</c> recovers the tree (the counit in
    /// interchange clothing); the other round trip lands at the same focus but FORGETS the
    /// upward context (severance is the cofree forgetting -- deliberate, and the reason the
    /// two round trips are not symmetric).</summary>
    public static IAsyncWalkableTreenumerable<TValue, THandle> Subtree<TValue, THandle>(
      this AsyncTreeWalker<TValue, THandle> walker)
      => new AsyncSubtreeWalkable<TValue, THandle>(walker.Walkable, walker.Focus);
  }
}
