using Copse;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// The comonad's co-bind (docs/CATEGORY_THEORY_SURVEY.md §6): relabel every node by an
    /// arbitrary OBSERVATION of its focus. The observer receives the walkable and the handle,
    /// so it can consult anything reachable from that vantage -- depth, ancestor values,
    /// subtree facts -- which is exactly what streaming <c>Select</c> cannot see. The shape
    /// and the handles are untouched (extend relabels, never reshapes); the result is a
    /// walkable whose streaming half is the Walk adapter driving the source's adjacency under
    /// the observer's labeling. The scans are this operation restricted to observations that
    /// factor through a fold along the traversal order -- pinned by the scan-coherence law in
    /// WalkerComonadLawTests: RootfixScan's accumulations equal Extend of the root-path fold.
    ///
    /// <para>Laws (the Store comonad's, pinned): <c>Extend(extract)</c> is the identity;
    /// <c>extract</c> after <c>Extend(f)</c> recovers <c>f</c>; and extend co-associates --
    /// <c>w.Extend(g).Extend(f)</c> ≡ <c>w.Extend((w0, h) => f(w0.Extend(g), h))</c>.</para>
    /// </summary>
    public static IWalkableTreenumerable<TResult, THandle> Extend<TValue, THandle, TResult>(
      this IWalkableTreenumerable<TValue, THandle> source,
      Func<IWalkableTreenumerable<TValue, THandle>, THandle, TResult> observer)
      => new ExtendWalkable<TValue, THandle, TResult>(source, observer);
  }
}
