using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// Async analog of <c>Copse.IChildEnumerator&lt;THandle&gt;</c>: the pull yields the next
  /// child's HANDLE -- the navigable identity the engine walks -- by value, as an
  /// <see cref="Option{TValue}"/> over <see cref="HandleAndSiblingIndex{THandle}"/> (an
  /// <c>out</c> param cannot cross an <c>await</c>, and a by-value result stores nothing
  /// between pulls). Its sync twin is <c>Copse.IChildEnumerator</c> over the same option, so
  /// the async-&gt;sync transcription is a pure <c>await</c>-strip.
  ///
  /// <para>Requires <see cref="IDisposable"/> in addition to <see cref="IAsyncDisposable"/> so the path
  /// (which disposes enumerators synchronously in this prototype) can tear them down. Proper async
  /// disposal is a follow-up that inverts disposal to the driver.</para>
  /// </summary>
  public interface IAsyncChildEnumerator<THandle> : IDisposable, IAsyncDisposable
  {
    /// <summary>The next child's handle with its sibling index, or absent past the last
    /// child.</summary>
    ValueTask<Option<HandleAndSiblingIndex<THandle>>> MoveNextAsync();
  }
}
