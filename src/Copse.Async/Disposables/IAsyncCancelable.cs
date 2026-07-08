// The async dual of the lifted ICancelable (System.Reactive's disposable algebra, see
// Copse.Primitives/Disposables). Not a verbatim AsyncRx.NET lift: AsyncRx's IAsyncCancelable is
// shaped for its async-everywhere gates; this is the direct transcription of the sync interface,
// consistent with the library's codegen naming (IAsyncDisposable is what IDisposable transcribes to).

using System;

namespace Copse.Disposables
{
  /// <summary>
  /// Async disposable resource with disposal state tracking.
  /// </summary>
  public interface IAsyncCancelable : IAsyncDisposable
  {
    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    bool IsDisposed { get; }
  }
}
