using System;

namespace Copse.Engine
{
  /// <summary>
  /// A synchronous, forward-only child enumerator in the <b>Current-property</b> style (a successful
  /// <see cref="MoveNext"/> exposes the child on <see cref="Current"/>), rather than the engine's
  /// historical <c>bool MoveNext(out ...)</c> style.
  ///
  /// <para>Exists because the async→sync codegen wants the pull to have the <b>same shape</b> in both
  /// colors: <c>IAsyncChildEnumerator</c> yields via <c>Current</c> (an <c>out</c> can't cross an
  /// <c>await</c>), so the generated sync twin does too -- making the pull line a pure rename
  /// (<c>MoveNextAsync</c> → <c>MoveNext</c>) with no structural seam rewrite. Adopting a uniform
  /// Current-style pull is the small design cost of the codegen approach.</para>
  /// </summary>
  public interface IForwardChildEnumerator<TNode> : IDisposable
  {
    bool MoveNext();

    NodeAndSiblingIndex<TNode> Current { get; }
  }
}
