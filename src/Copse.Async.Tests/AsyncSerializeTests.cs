using Copse.Core;
using Copse.Core.Async;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS check for the async serialize surface (the writer logic is tested once, on
  // the generated sync twins, by the round-trip suites): each grammar's async writer must emit
  // the exact bytes the sync writer emits for the same tree, from a genuinely-suspending source,
  // and round-trip back through the async deserializer.
  [TestClass]
  public class AsyncSerializeTests
  {
    private static readonly string[] Trees =
    {
      "a", "a(b,c,d)", "a(b(e),c)", "a,b,c", "a(b(d,e),c)", "a(b(d,e,f),c(g,h,i))",
      "\"qu\"\"oted\"(\"с 🌲\",\"(\")", "\"\"(x,\"\")",
    };

    [TestMethod]
    public async Task SerializeDepthFirst_MatchesSync()
    {
      foreach (var tree in Trees)
      {
        var sync = Sync(tree).SerializeDepthFirstTree();

        var writer = new StringWriter();
        await Async(tree).SerializeDepthFirstTreeAsync(writer);

        Assert.AreEqual(sync, writer.ToString(), $"dft {tree}");
      }
    }

    [TestMethod]
    public async Task SerializeBreadthFirst_MatchesSync()
    {
      foreach (var tree in Trees)
      {
        var sync = ((IBreadthFirstTreenumerable<string>)Sync(tree)).SerializeBreadthFirstTree();

        var writer = new StringWriter();
        await ((IAsyncBreadthFirstTreenumerable<string>)Async(tree)).SerializeBreadthFirstTreeAsync(writer);

        Assert.AreEqual(sync, writer.ToString(), $"bft {tree}");
      }
    }

    [TestMethod]
    public async Task AsyncRoundTrip_SerializeThenDeserialize_PreservesTraversal()
    {
      foreach (var tree in Trees)
      {
        var writer = new StringWriter();
        await Async(tree).SerializeDepthFirstTreeAsync(writer);
        var payload = writer.ToString();

        var replayed = TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(payload));

        var expected = Sync(tree).SerializeDepthFirstTree();
        var roundTripWriter = new StringWriter();
        await replayed.SerializeDepthFirstTreeAsync(roundTripWriter);

        Assert.AreEqual(expected, roundTripWriter.ToString(), $"round-trip {tree}");
      }
    }

    private static ITreenumerable<string> Sync(string tree) => TreeSerializer.DeserializeDepthFirstTree(tree);
    private static IAsyncTreenumerable<string> Async(string tree) => new YieldingAsyncSource<string>(Sync(tree));

    // A genuinely-suspending facade: Task.Yield on every pull, so the writers' awaits actually suspend.
    private sealed class YieldingAsyncSource<T> : IAsyncTreenumerable<T>
    {
      private readonly ITreenumerable<T> _Source;
      public YieldingAsyncSource(ITreenumerable<T> source) { _Source = source; }
      public IAsyncTreenumerator<T> GetAsyncDepthFirstTreenumerator() => new Cursor(_Source.GetDepthFirstTreenumerator());
      public IAsyncTreenumerator<T> GetAsyncBreadthFirstTreenumerator() => new Cursor(_Source.GetBreadthFirstTreenumerator());

      private sealed class Cursor : IAsyncTreenumerator<T>
      {
        private readonly ITreenumerator<T> _Inner;
        public Cursor(ITreenumerator<T> inner) { _Inner = inner; }
        public T Node => _Inner.Node;
        public int VisitCount => _Inner.VisitCount;
        public TreenumeratorMode Mode => _Inner.Mode;
        public NodePosition Position => _Inner.Position;
        public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s)
        {
          await Task.Yield(); // force real asynchrony on the pull seam
          return _Inner.MoveNext(s);
        }
        public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
      }
    }
  }
}
