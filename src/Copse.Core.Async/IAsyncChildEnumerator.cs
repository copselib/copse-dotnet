using System;
using System.Threading.Tasks;

namespace Copse.Core.Async
{
  /// <summary>
  /// Async analog of <c>Copse.IChildEnumerator&lt;TNode&gt;</c>. Because <c>out</c> parameters cannot
  /// cross an <c>await</c>, the yielded child is read from <see cref="Current"/> after a successful
  /// <see cref="MoveNextAsync"/> rather than returned via <c>out</c>.
  ///
  /// <para>Requires <see cref="IDisposable"/> in addition to <see cref="IAsyncDisposable"/> so the
  /// shared cadence (which disposes enumerators synchronously in this prototype) can tear them down.
  /// Proper async disposal is a follow-up that inverts disposal to the driver.</para>
  /// </summary>
  public interface IAsyncChildEnumerator<TNode> : IDisposable, IAsyncDisposable
  {
    ValueTask<bool> MoveNextAsync();

    NodeAndSiblingIndex<TNode> Current { get; }
  }
}
