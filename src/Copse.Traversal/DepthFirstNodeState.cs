using Copse.Core;

namespace Copse.Traversal
{
  /// <summary>The visit-state of one accepted node on the depth-first path: the node (already
  /// resolved from its handle at push), its position, and its live visit count -- a mutable
  /// visit record, ticked in place through the path's refs.</summary>
  internal struct DepthFirstNodeState<TNode>
  {
    public DepthFirstNodeState(TNode node, NodePosition position)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
    }

    public TNode Node;
    public NodePosition Position;
    public int VisitCount;
  }
}
