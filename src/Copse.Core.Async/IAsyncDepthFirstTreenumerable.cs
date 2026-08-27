namespace Copse.Core.Async
{
  /// <summary>Async analog of <c>IDepthFirstTreenumerable</c>: a source that affords a depth-first async traversal.</summary>
  public interface IAsyncDepthFirstTreenumerable<TNode>
  {
    /// <summary>Acquires a fresh depth-first traversal. The caller owns the treenumerator and disposes it.</summary>
    IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator();
  }
}
