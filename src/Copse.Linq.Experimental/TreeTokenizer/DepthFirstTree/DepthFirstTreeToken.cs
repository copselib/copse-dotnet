namespace Copse.Linq.Experimental.TreeTokenizer.DepthFirstTree
{
  public class DepthFirstTreeToken<TNode> : ITreeToken<TNode, DepthFirstTreeTokenType>
  {
    public DepthFirstTreeToken(DepthFirstTreeTokenType type)
    {
      Type = type;
      Node = default;
    }

    public DepthFirstTreeToken(TNode node)
    {
      Type = DepthFirstTreeTokenType.Node;
      Node = node;
    }

    public DepthFirstTreeTokenType Type { get; }
    public TNode Node { get; }

    public override string ToString()
    {
      switch (Type)
      {
        case DepthFirstTreeTokenType.StartChildGroup:
          return "(";
        case DepthFirstTreeTokenType.EndChildGroup:
          return ")";
        default:
          return Node.ToString();
      }
    }
  }
}
