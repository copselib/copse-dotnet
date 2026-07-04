using Copse.Core;
using Copse.Linq.TreeEnumerable.DepthFirstTree;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IDepthFirstTreeEnumerable<TNode> ToDepthFirstTreeEnumerable<TNode>(
      this IDepthFirstTreenumerable<TNode> source)
    {
      return new DepthFirstTreeEnumerable<TNode>(source);
    }
  }
}
