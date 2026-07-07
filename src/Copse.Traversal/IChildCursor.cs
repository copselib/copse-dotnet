using System;

namespace Copse.Traversal
{
  /// <summary>
  /// The result of a child pull: whether a child was yielded and, if so, which one. A named struct
  /// (not a tuple) so the contract is self-documenting: when <see cref="HasChild"/> is false,
  /// <see cref="Child"/> is <c>default</c> and must not be read.
  /// </summary>
  public readonly struct ChildResult<TNode>
  {
    public ChildResult(NodeAndSiblingIndex<TNode> child)
    {
      HasChild = true;
      Child = child;
    }

    public readonly bool HasChild;
    public readonly NodeAndSiblingIndex<TNode> Child;
  }

  /// <summary>
  /// A child enumerator that yields the next child BY VALUE (<see cref="ChildResult{TNode}"/>) rather
  /// than through an <c>out</c> param (<c>IChildEnumerator</c>) or a stored <c>Current</c>
  /// (<c>IForwardChildEnumerator</c>).
  ///
  /// <para>This is the shape that unifies sync and async: it stores NOTHING between pulls (so the
  /// enumerator struct stays small -- no frame bloat, unlike Current-style), AND it is legal in an
  /// async method (unlike <c>out</c>). The candidate pull contract if the parity A/B shows return-by-
  /// value doesn't cost the sync hot path vs <c>out</c>.</para>
  /// </summary>
  public interface IChildCursor<TNode> : IDisposable
  {
    ChildResult<TNode> MoveNext();
  }
}
