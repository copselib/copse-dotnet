using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Extensions
{
  public static class TreenumeratorExtensions
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeVisit<TNode> ToNodeVisit<TNode>(this ITreenumerator<TNode> treenumerator)
    {
      return
        new NodeVisit<TNode>(
          treenumerator.Mode,
          treenumerator.Node,
          treenumerator.VisitCount,
          treenumerator.Position);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeContext<TNode> ToNodeContext<TNode>(this ITreenumerator<TNode> treenumerator)
    {
      return
        new NodeContext<TNode>(
          treenumerator.Node,
          treenumerator.Position);
    }
  }
}
