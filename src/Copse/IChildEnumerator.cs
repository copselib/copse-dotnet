using System;

namespace Copse
{
  /// <summary>
  /// A synchronous child enumerator: the pull yields the next child's HANDLE -- the navigable
  /// identity the engine walks (an index into a store, an object reference, a handle-and-payload
  /// pair) -- by value, as an <see cref="Option{TValue}"/> over
  /// <see cref="NodeAndSiblingIndex{THandle}"/>. It stores no <c>Current</c> and uses no
  /// <c>out</c> param, so it holds nothing between pulls.
  ///
  /// <para>The sync half of the unified pull shape: the codegen'd sync twin of an async engine
  /// drives this, and the async source drives <c>IAsyncChildEnumerator</c> -- both over
  /// the same option, so the transcription is a pure <c>await</c>-strip.</para>
  /// </summary>
  public interface IChildEnumerator<THandle> : IDisposable
  {
    /// <summary>The next child's handle with its sibling index, or absent past the last
    /// child.</summary>
    Option<NodeAndSiblingIndex<THandle>> MoveNext();
  }
}
