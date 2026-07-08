using Copse.Linq.TreeTokenizer.DepthFirstTree;
using System.Collections.Generic;

namespace Copse.Linq.Async.TreeTokenizer.DepthFirstTree
{
  public interface IAsyncDepthFirstTreeTokenizer<TNode> : IAsyncEnumerable<DepthFirstTreeToken<TNode>>
  {
  }
}
