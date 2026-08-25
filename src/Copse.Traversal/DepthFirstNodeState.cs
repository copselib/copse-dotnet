using Copse.Core;

namespace Copse.Traversal
{
  /// <summary>The visit-state of one accepted node on the depth-first path.</summary>
  internal struct DepthFirstNodeState<THandle>
  {
    public DepthFirstNodeState(THandle node, NodePosition position)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
    }

    public THandle Node;
    public NodePosition Position;
    public int VisitCount;
  }
}
