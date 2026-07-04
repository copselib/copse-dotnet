// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information.
//
// Lifted from System.Reactive (github.com/dotnet/reactive, MIT) 2026-07-04, per the
// lift-verbatim policy: Rx's disposable algebra, name and semantics intact, so there are
// no new concepts for users. Namespace System.Reactive.Disposables -> Copse.Disposables.
// See THIRD-PARTY-NOTICES.md.

using System;

namespace Copse.Disposables
{
  /// <summary>
  /// Disposable resource with disposal state tracking.
  /// </summary>
  public interface ICancelable : IDisposable
  {
    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    bool IsDisposed { get; }
  }
}
