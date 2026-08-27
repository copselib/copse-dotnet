namespace Copse.Core.Async
{
  /// <summary>Async analog of <c>IBreadthFirstTreenumerable</c>: a source that affords a breadth-first async traversal.</summary>
  public interface IAsyncBreadthFirstTreenumerable<TNode>
  {
    /// <summary>Acquires a fresh breadth-first traversal. The caller owns the treenumerator and disposes it.</summary>
    IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator();
  }
}
