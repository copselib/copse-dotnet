namespace Copse.Core.Async
{
  /// <summary>
  /// Async analog of <c>ITreenumerable</c> -- a factory for async traversal cursors. Pared to the
  /// depth-first dimension for the prototype; the full traversal-dimension split (a breadth-first
  /// counterpart, narrow single-dimension interfaces) mirrors the synchronous design and is deferred.
  /// </summary>
  public interface IAsyncTreenumerable<TNode>
  {
    IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator();
  }
}
