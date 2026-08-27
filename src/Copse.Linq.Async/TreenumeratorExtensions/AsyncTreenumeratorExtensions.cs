using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Extensions
{
  // Async twin of TreenumeratorExtensions (a hand-written support pair, like the base/wrapper):
  // it exists so async operator sources can mirror the sync ones by calling
  // InnerTreenumerator.ToNodeAndPosition()/.ToNodeVisit(). The codegen renames
  // IAsyncTreenumerator -> ITreenumerator, so the generated twin's call resolves to the SYNC
  // TreenumeratorExtensions (same namespace) -- there is no generated copy of this class.
  /// <summary>Reads of a treenumerator's current visit as the record types.</summary>
  public static class AsyncTreenumeratorExtensions
  {
    /// <summary>The current visit as a <see cref="NodeVisit{TNode}"/> record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeVisit<TNode> ToNodeVisit<TNode>(this IAsyncTreenumerator<TNode> treenumerator)
    {
      return
        new NodeVisit<TNode>(
          treenumerator.Node,
          treenumerator.VisitCount,
          treenumerator.Position);
    }

    /// <summary>The current node with its position, as a <see cref="NodeAndPosition{TNode}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeAndPosition<TNode> ToNodeAndPosition<TNode>(this IAsyncTreenumerator<TNode> treenumerator)
    {
      return
        new NodeAndPosition<TNode>(
          treenumerator.Node,
          treenumerator.Position);
    }
  }
}
