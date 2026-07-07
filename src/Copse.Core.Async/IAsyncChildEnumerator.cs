using System;
using System.Threading.Tasks;

namespace Copse.Core.Async
{
  /// <summary>
  /// Async analog of <c>Copse.IChildEnumerator&lt;TNode&gt;</c>. The pull returns the next child BY VALUE
  /// (<see cref="ChildResult{TNode}"/>) -- an <c>out</c> param can't cross an <c>await</c>, and a
  /// by-value result stores nothing between pulls (so the enumerator struct stays small, unlike a
  /// stored <c>Current</c>). Its sync twin is <c>Copse.Traversal.IChildCursor</c> over the same
  /// <see cref="ChildResult{TNode}"/>, so the async-&gt;sync transcription is a pure <c>await</c>-strip.
  ///
  /// <para>Requires <see cref="IDisposable"/> in addition to <see cref="IAsyncDisposable"/> so the path
  /// (which disposes enumerators synchronously in this prototype) can tear them down. Proper async
  /// disposal is a follow-up that inverts disposal to the driver.</para>
  /// </summary>
  public interface IAsyncChildEnumerator<TNode> : IDisposable, IAsyncDisposable
  {
    ValueTask<ChildResult<TNode>> MoveNextAsync();
  }
}
