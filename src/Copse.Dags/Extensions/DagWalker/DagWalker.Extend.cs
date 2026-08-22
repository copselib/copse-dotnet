using System;

namespace Copse.Dags
{
  /// <summary>The walker-receiver operators: the comonad at the focused presentation.</summary>
  public static partial class DagWalker
  {
    /// <summary>
    /// Co-bind on the carrier: relabel the whole topology by an observation of every focus, and
    /// keep standing where you are -- the unfocused stance included. The observer receives THE
    /// WALKER (the vantage as a value); observers fire at nodes. The completed extend's one
    /// remaining row is <c>observer(unfocusedWalker)</c>, a direct application, never an
    /// operator (CATEGORY_THEORY_SURVEY.md §12, carrier-neutral).
    /// </summary>
    public static DagWalker<TResult, THandle, TEdge> Extend<TValue, THandle, TEdge, TResult>(
      this DagWalker<TValue, THandle, TEdge> walker,
      Func<DagWalker<TValue, THandle, TEdge>, TResult> observer)
    {
      if (observer == null)
        throw new ArgumentNullException(nameof(observer));

      var relabeled = new DagExtendWalkable<TValue, THandle, TEdge, TResult>(
        walker.Topology,
        (topology, handle) => observer(new DagWalker<TValue, THandle, TEdge>(topology, handle)));

      return walker.HasFocus
        ? new DagWalker<TResult, THandle, TEdge>(relabeled, walker.Focus)
        : new DagWalker<TResult, THandle, TEdge>(relabeled);
    }
  }
}
