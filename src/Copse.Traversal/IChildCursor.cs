using System;

namespace Copse.Traversal
{
  /// <summary>
  /// A synchronous child enumerator that yields the next child BY VALUE
  /// (<see cref="ChildResult{TNode}"/>) from <see cref="MoveNext"/>, rather than through an <c>out</c>
  /// param (<c>IChildEnumerator</c>) or a stored <c>Current</c>.
  ///
  /// <para>The sync half of the unified pull shape: the codegen'd sync twin of an async engine drives
  /// this, and the async source drives <c>IAsyncChildEnumerator</c> -- both over
  /// <see cref="ChildResult{TNode}"/>, so the transcription is a pure <c>await</c>-strip. Stores
  /// nothing between pulls, so it matches the <c>out</c>-style engine's allocation.</para>
  /// </summary>
  public interface IChildCursor<TNode> : IDisposable
  {
    ChildResult<TNode> MoveNext();
  }
}
