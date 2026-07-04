namespace Copse.Core
{
  public interface IBreadthFirstTreenumerable<TNode>
  {
    ITreenumerator<TNode> GetBreadthFirstTreenumerator();
  }
}
