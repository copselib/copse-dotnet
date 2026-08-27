using Copse;
using Copse.Linq.Treenumerables;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreeWalker
  {
    /// <summary>
    /// Co-bind on the carrier: relabel the whole topology by an observation of every focus,
    /// and keep standing where you are -- the unfocused stance included (the result walker stands
    /// where this one does). The observer receives a walker, so it can extract, step, and
    /// extend -- anything a stance affords. Observers fire at NODES: this is the interior
    /// part of the completed extend, whose one remaining row -- the observation AT the unfocused stance
    /// -- is a direct application, <c>observer(unfocusedWalker)</c>, never an operator
    /// (CATEGORY_THEORY_SURVEY.md §12). An extension rather than a struct member by the
    /// carriers-in-Core/algebras-in-Linq split: the result is a walkable, walkables stream,
    /// and streaming needs the Walk adapter -- which lives here, not in Core (the same
    /// reason <c>ITreenumerable</c> carries no <c>Select</c>).
    /// </summary>
    public static AsyncTreeWalker<TResult, THandle> Extend<TNode, THandle, TResult>(
      this AsyncTreeWalker<TNode, THandle> walker,
      Func<AsyncTreeWalker<TNode, THandle>, ValueTask<TResult>> observer)
    {
      var relabeled = new AsyncExtendWalkable<TNode, THandle, TResult>(
        walker.Topology,
        (topology, handle) => observer(new AsyncTreeWalker<TNode, THandle>(topology, handle)));

      return !walker.HasFocus
        ? new AsyncTreeWalker<TResult, THandle>(relabeled)
        : new AsyncTreeWalker<TResult, THandle>(relabeled, walker.Focus);
    }
  }
}
