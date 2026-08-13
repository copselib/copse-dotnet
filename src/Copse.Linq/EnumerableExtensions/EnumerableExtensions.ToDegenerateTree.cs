using Copse.Treenumerables;
﻿using Copse.Core;
using Copse.Linq.Treenumerators.Enumerator;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class EnumerableExtensions
  {
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
