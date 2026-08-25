namespace Copse.Core
{
  /// <summary>A source that affords a breadth-first traversal: all nodes at one depth are
  /// scheduled before any node at the next depth is visited.</summary>
  public interface IBreadthFirstTreenumerable<TNode>
  {
    /// <summary>Creates a fresh breadth-first traversal cursor over this tree.</summary>
    ITreenumerator<TNode> GetBreadthFirstTreenumerator();
  }
}
