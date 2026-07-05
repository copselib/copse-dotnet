namespace Copse.Core
{
  public interface IDepthFirstTreenumerable<TNode>
  {
    ITreenumerator<TNode> GetDepthFirstTreenumerator();
  }
}
