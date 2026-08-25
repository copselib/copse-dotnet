namespace Copse.Core
{
  /// <summary>A source that affords a depth-first traversal: each child's entire subtree
  /// completes before its next sibling is scheduled.</summary>
  public interface IDepthFirstTreenumerable<TNode>
  {
    /// <summary>Creates a fresh depth-first traversal cursor over this tree.</summary>
    ITreenumerator<TNode> GetDepthFirstTreenumerator();
  }
}
