using Copse.Linq.TreeTokenizer.BreadthFirstTree;
using System.Collections.Generic;

namespace Copse.Linq.Async.TreeTokenizer.BreadthFirstTree
{
  public interface IAsyncBreadthFirstTreeTokenizer<TNode> : IAsyncEnumerable<BreadthFirstTreeToken<TNode>>
  {
  }
}
