using Copse.Linq.Experimental.TreeTokenizer.DepthFirstTree;
using System.Collections.Generic;

namespace Copse.Linq.Experimental.TreeTokenizer.DepthFirstTree
{
  public interface IDepthFirstTreeTokenizer<TNode> : IEnumerable<DepthFirstTreeToken<TNode>>
  {
  }
}
