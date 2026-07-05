using Copse.Core;
using Copse.Treenumerators;

namespace Copse.Treenumerables
{
  internal class EmptyTreenumerable<TNode> : ITreenumerable<TNode>
  {
    private EmptyTreenumerable()
    {
    }

    public static EmptyTreenumerable<TNode> Instance { get; } = new EmptyTreenumerable<TNode>();

    public ITreenumerator<TNode> GetBreadthFirstTreenumerator()
      => EmptyTreenumerator<TNode>.Instance;

    public ITreenumerator<TNode> GetDepthFirstTreenumerator()
      => EmptyTreenumerator<TNode>.Instance;
  }
}
