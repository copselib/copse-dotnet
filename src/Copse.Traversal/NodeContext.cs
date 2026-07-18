using Copse.Core;

namespace Copse
{
  public readonly struct NodeContext<TNode>
  {
    public NodeContext(
      TNode node,
      NodePosition position)
    {
      Node = node;
      Position = position;
    }

    public readonly TNode Node;
    public readonly NodePosition Position;

    public override string ToString() => $"{Position}  {Node}";
  }
}
