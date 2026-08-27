using Copse.Treenumerables;
using Copse.Core;
using Copse.Linq.Treenumerators.Enumerator;
using System.Collections.Generic;

namespace Copse.Linq
{
  /// <summary>Doors from flat sequences into trees.</summary>
  public static partial class EnumerableExtensions
  {
    /// <summary>The sequence as a single unary chain: each element the sole child of the one
    /// before it. Deferred.</summary>
    public static ITreenumerable<TNode> ToDegenerateTree<TNode>(this IEnumerable<TNode> source)
    {
      return
        Tree
        .Create(
          () => new EnumerableAsTreeBreadthFirstTreenumerator<TNode>(source),
          () => new EnumerableAsTreeDepthFirstTreenumerator<TNode>(source));
    }
  }
}
