using System;

namespace Copse
{
  /// <summary>
  /// A synchronous child enumerator that yields the next child BY VALUE
  /// (<see cref="ChildResult{TNode}"/>) from <see cref="MoveNext"/> -- it stores no
  /// <c>Current</c> and uses no <c>out</c> param, so it holds nothing between pulls (matching the
  /// allocation profile of an out-style engine).
  ///
  /// <para>The sync half of the unified pull shape: the codegen'd sync twin of an async engine
  /// drives this, and the async source drives <c>IAsyncChildEnumerator</c> -- both over
  /// <see cref="ChildResult{TNode}"/>, so the transcription is a pure <c>await</c>-strip.</para>
  /// </summary>
  public interface IChildEnumerator<TNode> : IDisposable
  {
    ChildResult<TNode> MoveNext();
  }
}
