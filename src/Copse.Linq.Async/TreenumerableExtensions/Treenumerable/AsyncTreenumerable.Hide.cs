using Copse.Linq.Treenumerators;
using Copse;
using Copse.Treenumerables;
using Copse.Core;
using Copse.Linq;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type -- which also makes it a composition barrier, since operators
    /// compose by probing the treenumerable. Deferred.
    /// <para>
    /// This overload hides both layers (<see cref="HideScope.Treenumerator"/>) -- the safe default
    /// for a defensive operator, so callers opt OUT rather than in. <c>Hide(HideScope.Treenumerable)</c>
    /// is the same composition barrier at no per-pull cost, for callers who accept that the concrete
    /// machine type stays visible; see <see cref="HideScope"/> for why that is not the default.
    /// </para>
    /// </summary>
    public static IAsyncTreenumerable<TNode> Hide<TNode>(
      this IAsyncTreenumerable<TNode> source)
      => Hide(source, HideScope.Treenumerator);

    /// <summary>Async <c>Hide</c> to an explicit <see cref="HideScope"/>.</summary>
    public static IAsyncTreenumerable<TNode> Hide<TNode>(
      this IAsyncTreenumerable<TNode> source,
      HideScope scope)
      => new AsyncHideTreenumerable<TNode>(source, scope);

    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type -- which also makes it a composition barrier, since operators
    /// compose by probing the treenumerable. Deferred.
    /// <para>
    /// This overload hides both layers (<see cref="HideScope.Treenumerator"/>) -- the safe default
    /// for a defensive operator, so callers opt OUT rather than in. <c>Hide(HideScope.Treenumerable)</c>
    /// is the same composition barrier at no per-pull cost, for callers who accept that the concrete
    /// machine type stays visible; see <see cref="HideScope"/> for why that is not the default.
    /// </para>
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> Hide<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source)
      => Hide(source, HideScope.Treenumerator);

    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type -- which also makes it a composition barrier, since operators
    /// compose by probing the treenumerable. Deferred.
    /// <para>
    /// This overload hides both layers (<see cref="HideScope.Treenumerator"/>) -- the safe default
    /// for a defensive operator, so callers opt OUT rather than in. <c>Hide(HideScope.Treenumerable)</c>
    /// is the same composition barrier at no per-pull cost, for callers who accept that the concrete
    /// machine type stays visible; see <see cref="HideScope"/> for why that is not the default.
    /// </para>
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> Hide<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      HideScope scope)
    {
      // The narrow barrier needs no bespoke type: AsyncTree.CreateDepthFirst returns a
      // delegating treenumerable that claims nothing beyond its one dimension.
      if (scope == HideScope.Treenumerable)
        return AsyncTree.CreateDepthFirst(source.GetAsyncDepthFirstTreenumerator);

      return AsyncTree.CreateDepthFirst(
        () => new AsyncHideTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator));
    }

    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type -- which also makes it a composition barrier, since operators
    /// compose by probing the treenumerable. Deferred.
    /// <para>
    /// This overload hides both layers (<see cref="HideScope.Treenumerator"/>) -- the safe default
    /// for a defensive operator, so callers opt OUT rather than in. <c>Hide(HideScope.Treenumerable)</c>
    /// is the same composition barrier at no per-pull cost, for callers who accept that the concrete
    /// machine type stays visible; see <see cref="HideScope"/> for why that is not the default.
    /// </para>
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> Hide<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source)
      => Hide(source, HideScope.Treenumerator);

    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type -- which also makes it a composition barrier, since operators
    /// compose by probing the treenumerable. Deferred.
    /// <para>
    /// This overload hides both layers (<see cref="HideScope.Treenumerator"/>) -- the safe default
    /// for a defensive operator, so callers opt OUT rather than in. <c>Hide(HideScope.Treenumerable)</c>
    /// is the same composition barrier at no per-pull cost, for callers who accept that the concrete
    /// machine type stays visible; see <see cref="HideScope"/> for why that is not the default.
    /// </para>
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> Hide<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      HideScope scope)
    {
      if (scope == HideScope.Treenumerable)
        return AsyncTree.CreateBreadthFirst(source.GetAsyncBreadthFirstTreenumerator);

      return AsyncTree.CreateBreadthFirst(
        () => new AsyncHideTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator));
    }
  }
}
