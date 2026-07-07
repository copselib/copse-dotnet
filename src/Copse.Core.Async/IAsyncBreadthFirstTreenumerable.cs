namespace Copse.Core.Async
{
  /// <summary>Async analog of <c>IBreadthFirstTreenumerable</c>: a source that affords a breadth-first async traversal.</summary>
  public interface IAsyncBreadthFirstTreenumerable<TNode>
  {
    IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator();
  }
}
