using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  internal interface IAsyncSelectTreenumerable<TResult> : IAsyncTreenumerable<TResult>
  {
    IAsyncSelectTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> outerSelector);
  }
}
