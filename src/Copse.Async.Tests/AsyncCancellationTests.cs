using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Cancellation semantics at the serializer's I/O edges (the tokens deliberately do NOT ride
  // MoveNextAsync/DisposeAsync -- they bind at the Deserialize*Async/Serialize*Async calls):
  // the reader observes the token cooperatively at each block refill (per 4096 chars), the
  // writers once per emitted visit.
  [TestClass]
  public class AsyncCancellationTests
  {
    [TestMethod]
    public async Task Deserialize_PreCancelledToken_ThrowsOnFirstPull()
    {
      using var cancellation = new CancellationTokenSource();
      cancellation.Cancel();

      var tree = TreeSerializer.DeserializeDepthFirstTreeAsync(
        () => new StringReader("a(b,c)"), cancellation.Token);

      var treenumerator = tree.GetAsyncDepthFirstTreenumerator();
      await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll));
      await treenumerator.DisposeAsync();
    }

    [TestMethod]
    public async Task Deserialize_CancelledMidStream_ThrowsAtNextBlockRefill()
    {
      // A payload spanning several 4096-char blocks: wide forest of quoted values.
      var payload = new StringBuilder();
      for (var i = 0; i < 3000; i++)
      {
        if (i > 0)
          payload.Append(',');
        payload.Append("node").Append(i);
      }

      using var cancellation = new CancellationTokenSource();
      var tree = TreeSerializer.DeserializeDepthFirstTreeAsync(
        () => new StringReader(payload.ToString()), cancellation.Token);

      var treenumerator = tree.GetAsyncDepthFirstTreenumerator();
      Assert.IsTrue(await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll), "first block serves before cancel");

      cancellation.Cancel();

      // Cooperative: pulls keep serving from the CURRENT block; the throw lands when the
      // scanner needs the next refill.
      await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll)) { }
      });
      await treenumerator.DisposeAsync();
    }

    [TestMethod]
    public async Task Serialize_CancelledMidTraversal_Throws()
    {
      using var cancellation = new CancellationTokenSource();

      // Cancel from inside the traversal after a few visits: the writer's per-visit check
      // observes it on the next loop turn.
      var pulls = 0;
      var source = TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e)")
        .Do(_ => { if (++pulls == 3) cancellation.Cancel(); });

      var async = new AsyncFacade(source);
      var writer = new StringWriter();

      await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        await async.SerializeDepthFirstTreeAsync(writer, cancellation.Token));
    }

    [TestMethod]
    public async Task DefaultToken_NeverInterferes()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader("a(b,c)"));
      var writer = new StringWriter();
      await tree.SerializeDepthFirstTreeAsync(writer);
      Assert.AreEqual("a(b,c)", writer.ToString());
    }

    // A completed-ValueTask facade over a sync composite treenumerable.
    private sealed class AsyncFacade : IAsyncTreenumerable<string>
    {
      private readonly ITreenumerable<string> _Source;
      public AsyncFacade(ITreenumerable<string> source) { _Source = source; }
      public IAsyncTreenumerator<string> GetAsyncDepthFirstTreenumerator() => new Cursor(_Source.GetDepthFirstTreenumerator());
      public IAsyncTreenumerator<string> GetAsyncBreadthFirstTreenumerator() => new Cursor(_Source.GetBreadthFirstTreenumerator());

      private sealed class Cursor : IAsyncTreenumerator<string>
      {
        private readonly ITreenumerator<string> _Inner;
        public Cursor(ITreenumerator<string> inner) { _Inner = inner; }
        public string Node => _Inner.Node;
        public int VisitCount => _Inner.VisitCount;
        public TreenumeratorMode Mode => _Inner.Mode;
        public NodePosition Position => _Inner.Position;
        public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s) => new ValueTask<bool>(_Inner.MoveNext(s));
        public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
      }
    }
  }
}
