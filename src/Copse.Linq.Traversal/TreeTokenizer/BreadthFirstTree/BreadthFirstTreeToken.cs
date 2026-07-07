namespace Copse.Linq.TreeTokenizer.BreadthFirstTree
{
  public class BreadthFirstTreeToken<TNode> : ITreeToken<TNode, BreadthFirstTreeTokenType>
  {
    public BreadthFirstTreeToken(BreadthFirstTreeTokenType type)
    {
      Type = type;
      Node = default;
    }

    public BreadthFirstTreeToken(TNode node)
    {
      Type = BreadthFirstTreeTokenType.Node;
      Node = node;
    }

    public BreadthFirstTreeTokenType Type { get; }
    public TNode Node { get; }

    public override string ToString()
    {
      switch (Type)
      {
        case BreadthFirstTreeTokenType.GenerationSeparator:
          return "|";
        case BreadthFirstTreeTokenType.FamilySeparator:
          return ":";
        default:
          return Node.ToString();
      }
    }
  }
}
