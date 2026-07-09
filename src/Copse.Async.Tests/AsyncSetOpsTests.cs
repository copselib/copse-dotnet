using Copse.Core;
using Copse.Core.Async;
using Copse.Linq;
using Copse.Linq.Treenumerators; // MergeNode
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // The async set ops (Union/Intersection/Subtract/SymmetricDifference) are pure fluent compositions
  // over the async StructuralMerge + PruneBefore/Where/Select -- all already conformance-tested. This
  // validates the WIRING: the async composition must produce the same result as the trusted sync set op
  // over the same operand trees (both dimensions). Async operands are a facade over the sync composite
  // deserialize (so both sides read the identical tree).
  [TestClass]
  public class AsyncSetOpsTests
  {
    private static readonly (string Left, string Right)[] Pairs =
    {
      ("a(b,c,d)", "a(b)"),
      ("a(b(e),c)", "a(b,c(f))"),
      ("a,b,c", "a,b"),
      ("a(b,c)", "x(y,z)"),
      ("a(b(d,e),c)", "a(b(f),c(g,h))"),
      ("a", "a(b,c)"),
    };

    [TestMethod]
    public async Task Union_MatchesSync()
    {
      foreach (var (l, r) in Pairs)
      {
        CollectionAssert.AreEqual(
          Merges(SyncOf(l).Union(SyncOf(r)).GetDepthFirstTreenumerator()),
          await MergesAsync(AsyncOf(l).Union(AsyncOf(r)).GetAsyncDepthFirstTreenumerator()),
          $"Union DFT {l} x {r}");

        CollectionAssert.AreEqual(
          Merges(SyncOf(l).Union(SyncOf(r)).GetBreadthFirstTreenumerator()),
          await MergesAsync(AsyncOf(l).Union(AsyncOf(r)).GetAsyncBreadthFirstTreenumerator()),
          $"Union BFT {l} x {r}");
      }
    }

    [TestMethod]
    public async Task Intersection_And_SymmetricDifference_MatchSync()
    {
      foreach (var (l, r) in Pairs)
      {
        CollectionAssert.AreEqual(
          Merges(SyncOf(l).Intersection(SyncOf(r)).GetDepthFirstTreenumerator()),
          await MergesAsync(AsyncOf(l).Intersection(AsyncOf(r)).GetAsyncDepthFirstTreenumerator()),
          $"Intersection {l} x {r}");

        CollectionAssert.AreEqual(
          Merges(SyncOf(l).SymmetricDifference(SyncOf(r)).GetBreadthFirstTreenumerator()),
          await MergesAsync(AsyncOf(l).SymmetricDifference(AsyncOf(r)).GetAsyncBreadthFirstTreenumerator()),
          $"SymmetricDifference {l} x {r}");
      }
    }

    [TestMethod]
    public async Task Subtract_MatchesSync()
    {
      foreach (var (l, r) in Pairs)
        CollectionAssert.AreEqual(
          Values(SyncOf(l).Subtract(SyncOf(r)).GetDepthFirstTreenumerator()),
          await ValuesAsync(AsyncOf(l).Subtract(AsyncOf(r)).GetAsyncDepthFirstTreenumerator()),
          $"Subtract {l} x {r}");
    }

    private static ITreenumerable<string> SyncOf(string tree) => TreeSerializer.DeserializeDepthFirstTree(tree);
    private static IAsyncTreenumerable<string> AsyncOf(string tree) => new AsyncFacade<string>(SyncOf(tree));

    private static List<string> Merges(ITreenumerator<MergeNode<string, string>> t)
    {
      var v = new List<string>();
      using (t)
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            v.Add($"L{t.Node.HasLeft}:{t.Node.Left}|R{t.Node.HasRight}:{t.Node.Right}@{t.Position.Depth},{t.Position.SiblingIndex}");
      return v;
    }

    private static async Task<List<string>> MergesAsync(IAsyncTreenumerator<MergeNode<string, string>> t)
    {
      var v = new List<string>();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            v.Add($"L{t.Node.HasLeft}:{t.Node.Left}|R{t.Node.HasRight}:{t.Node.Right}@{t.Position.Depth},{t.Position.SiblingIndex}");
      return v;
    }

    private static List<string> Values(ITreenumerator<string> t)
    {
      var v = new List<string>();
      using (t)
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            v.Add($"{t.Node}@{t.Position.Depth},{t.Position.SiblingIndex}");
      return v;
    }

    private static async Task<List<string>> ValuesAsync(IAsyncTreenumerator<string> t)
    {
      var v = new List<string>();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            v.Add($"{t.Node}@{t.Position.Depth},{t.Position.SiblingIndex}");
      return v;
    }

    // A completed-ValueTask facade: presents a sync composite treenumerable as an async one (each pull
    // resolves synchronously). Lets the async set ops run over the exact same tree the sync ones read.
    private sealed class AsyncFacade<T> : IAsyncTreenumerable<T>
    {
      private readonly ITreenumerable<T> _Source;
      public AsyncFacade(ITreenumerable<T> source) { _Source = source; }
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
        public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies s) => new ValueTask<bool>(_Inner.MoveNext(s));
        public ValueTask DisposeAsync() { _Inner.Dispose(); return default; }
      }
    }
  }
}
