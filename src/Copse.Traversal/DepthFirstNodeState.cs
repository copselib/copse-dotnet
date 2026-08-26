using Copse.Core;

namespace Copse.Traversal
{
  /// <summary>The visit-state of one accepted node on the depth-first path.</summary>
  internal struct DepthFirstNodeState<THandle>
  {
    public DepthFirstNodeState(THandle handle, NodePosition position)
    {
      Handle = handle;
      Position = position;
      VisitCount = 0;
    }

    public THandle Handle;
    public NodePosition Position;
    public int VisitCount;
  }
}
