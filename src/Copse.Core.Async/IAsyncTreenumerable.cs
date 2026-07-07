namespace Copse.Core.Async
{
  /// <summary>
  /// Async analog of <c>ITreenumerable</c>: the pure composite of the two traversal-dimension
  /// interfaces -- an async tree that affordably offers BOTH traversal streams. Sources that afford
  /// only one dimension (e.g. a forward-only async serialized stream) implement the matching narrow
  /// interface (<see cref="IAsyncDepthFirstTreenumerable{TNode}"/> or
  /// <see cref="IAsyncBreadthFirstTreenumerable{TNode}"/>) instead.
  /// </summary>
  public interface IAsyncTreenumerable<TNode>
    : IAsyncDepthFirstTreenumerable<TNode>,
      IAsyncBreadthFirstTreenumerable<TNode>
  {
  }
}
