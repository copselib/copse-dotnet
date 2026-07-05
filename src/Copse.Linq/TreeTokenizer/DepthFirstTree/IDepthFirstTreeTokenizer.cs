using System.Collections.Generic;

namespace Copse.Linq.TreeTokenizer.DepthFirstTree
{
  public interface IDepthFirstTreeTokenizer<TNode> : IEnumerable<DepthFirstTreeToken<TNode>>
  {
  }
}
