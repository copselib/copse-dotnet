using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Extensions
{
  /// <summary>Reads of a treenumerator's current visit as the record types.</summary>
  public static class TreenumeratorExtensions
  {
    /// <summary>The current visit as a <see cref="NodeVisit{TNode}"/> record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeVisit<TNode> ToNodeVisit<TNode>(this ITreenumerator<TNode> treenumerator)
    {
      return
        new NodeVisit<TNode>(
          treenumerator.Node,
          treenumerator.VisitCount,
          treenumerator.Position);
    }

    /// <summary>The current node with its position, as a <see cref="NodeAndPosition{TNode}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeAndPosition<TNode> ToNodeAndPosition<TNode>(this ITreenumerator<TNode> treenumerator)
    {
      return
        new NodeAndPosition<TNode>(
          treenumerator.Node,
          treenumerator.Position);
    }
  }
}
