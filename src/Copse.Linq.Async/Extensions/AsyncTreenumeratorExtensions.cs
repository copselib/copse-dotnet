using Copse.Core;
using Copse.Core.Async;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Extensions
{
  // Async twin of TreenumeratorExtensions (a hand-written support pair, like the base/wrapper):
  // it exists so async operator sources can mirror the sync ones by calling
  // InnerTreenumerator.ToNodeContext()/.ToNodeVisit(). The codegen renames
  // IAsyncTreenumerator -> ITreenumerator, so the generated twin's call resolves to the SYNC
  // TreenumeratorExtensions (same namespace) -- there is no generated copy of this class.
  public static class AsyncTreenumeratorExtensions
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeVisit<TNode> ToNodeVisit<TNode>(this IAsyncTreenumerator<TNode> treenumerator)
    {
      return
        new NodeVisit<TNode>(
          treenumerator.Mode,
          treenumerator.Node,
          treenumerator.VisitCount,
          treenumerator.Position);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeContext<TNode> ToNodeContext<TNode>(this IAsyncTreenumerator<TNode> treenumerator)
    {
      return
        new NodeContext<TNode>(
          treenumerator.Node,
          treenumerator.Position);
    }
  }
}
