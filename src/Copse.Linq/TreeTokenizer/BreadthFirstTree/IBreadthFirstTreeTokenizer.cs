using System.Collections.Generic;

namespace Copse.Linq.TreeTokenizer.BreadthFirstTree
{
  public interface IBreadthFirstTreeTokenizer<TNode> : IEnumerable<BreadthFirstTreeToken<TNode>>
  {
  }
}
