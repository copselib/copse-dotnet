using Copse.Treenumerables;
using Copse.Core;
using Copse.Linq.Treenumerators.Enumerator;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class EnumerableExtensions
  {
    public static ITreenumerable<TNode> ToTrivialForest<TNode>(this IEnumerable<TNode> source)
    {
      return
        Tree
        .Create(
          () => new EnumerableAsForestTreenumerator<TNode>(source),
          () => new EnumerableAsForestTreenumerator<TNode>(source));
    }
  }
}
