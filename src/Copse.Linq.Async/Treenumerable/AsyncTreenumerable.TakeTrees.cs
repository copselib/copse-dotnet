using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Keeps only the first <paramref name="count"/> root trees (stops at the root whose sibling index reaches count).</summary>
    public static IAsyncTreenumerable<TNode> TakeTrees<TNode>(this IAsyncTreenumerable<TNode> source, int count)
      => source.TakeNodesUntil(
        nc => nc.Position.Depth == 0 && nc.Position.SiblingIndex == count,
        false);
  }
}
