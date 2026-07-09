using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  // The async release of a Using resource. The constraint is IDisposable (readers -- the
  // flagship Using resource -- never grew IAsyncDisposable), but a resource that ALSO implements
  // IAsyncDisposable gets its async disposal preferred (a connection or stream may flush over
  // I/O on release) -- the same dual dispatch ASP.NET Core's service scopes use. Async-only: in
  // the sync twin the preference is vestigial by construction, so the transcribed call sites
  // substitute a plain Dispose instead (see the codegen marker regions in AsyncTree).
  internal static class AsyncResourceDisposal
  {
    public static async ValueTask DisposeAsync<TResource>(TResource resource)
      where TResource : IDisposable
    {
      if (resource is IAsyncDisposable asyncDisposable)
      {
        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        return;
      }

      resource.Dispose();
    }
  }
}
