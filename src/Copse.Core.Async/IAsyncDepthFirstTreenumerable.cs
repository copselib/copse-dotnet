namespace Copse.Core.Async
{
  /// <summary>Async analog of <c>IDepthFirstTreenumerable</c>: a source that affords a depth-first async traversal.</summary>
  public interface IAsyncDepthFirstTreenumerable<TNode>
  {
    IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator();
  }
}
