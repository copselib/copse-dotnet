using Copse.Linq.Experimental.TreeTokenizer.BreadthFirstTree;
using System.Collections.Generic;

namespace Copse.Linq.Experimental.TreeTokenizer.BreadthFirstTree
{
  public interface IBreadthFirstTreeTokenizer<TNode> : IEnumerable<BreadthFirstTreeToken<TNode>>
  {
  }
}
