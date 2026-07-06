using Copse;
using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Treenumerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Async.Smoke;

internal static class Program
{
  // A small forest to traverse:
  //
  //        1              6
  //      / | \            |
  //     2  3  4           7
  //        |
  //        5
  private static readonly Dictionary<int, int[]> Tree = new()
  {
    [1] = new[] { 2, 3, 4 },
    [3] = new[] { 5 },
    [6] = new[] { 7 },
  };
  private static readonly int[] Roots = { 1, 6 };

  private static int[] ChildrenOf(int node) => Tree.TryGetValue(node, out var c) ? c : Array.Empty<int>();

  private static async Task<int> Main()
  {
    var sync = RunSync();
    var async = await RunAsync();

    Console.WriteLine($"sync  visits: {sync.Count}");
    Console.WriteLine($"async visits: {async.Count}");

    bool streamsMatch = sync.SequenceEqual(async);
    Console.WriteLine($"visit streams identical: {streamsMatch}");

    if (!streamsMatch)
    {
      Console.WriteLine("\nfirst divergence:");
      int n = Math.Max(sync.Count, async.Count);
      for (int i = 0; i < n; i++)
      {
        var s = i < sync.Count ? sync[i].ToString() : "<none>";
        var a = i < async.Count ? async[i].ToString() : "<none>";
        if (s != a) { Console.WriteLine($"  [{i}] sync={s}  async={a}"); break; }
      }
    }
    else
    {
      Console.WriteLine("\nvisit stream (Mode Node vVisitCount @depth,sib):");
      foreach (var v in sync)
        Console.WriteLine($"  {v}");
    }

    // Async operator composition: Select(n => n * 10) must map values and preserve the stream shape.
    var mapped = await RunAsyncSelect();
    var expected = sync.Where(v => v.Mode == TreenumeratorMode.VisitingNode && v.VisitCount == 1)
                       .Select(v => v.Node * 10)
                       .ToList();
    var gotNodes = mapped.Where(v => v.Mode == TreenumeratorMode.VisitingNode && v.VisitCount == 1)
                         .Select(v => v.Node)
                         .ToList();
    bool selectOk = expected.SequenceEqual(gotNodes);
    Console.WriteLine($"\nasync Select(n*10) first-visit nodes: [{string.Join(", ", gotNodes)}]");
    Console.WriteLine($"async Select maps + preserves shape: {selectOk}");

    bool pass = streamsMatch && selectOk;
    Console.WriteLine($"\n{(pass ? "PASS" : "FAIL")}");
    return pass ? 0 : 1;
  }

  private readonly record struct Visit(TreenumeratorMode Mode, int Node, int VisitCount, int Depth, int SiblingIndex)
  {
    public override string ToString()
    {
      var m = Mode == TreenumeratorMode.SchedulingNode ? "S" : "V";
      return $"{m} {Node} v{VisitCount} @{Depth},{SiblingIndex}";
    }
  }

  // --- Sync engine over an in-memory tree (the oracle). ---

  private static List<Visit> RunSync()
  {
    var t = new DepthFirstTreenumerator<int, int, SyncChildEnumerator>(
      Roots,
      nc => new SyncChildEnumerator(ChildrenOf(nc.Node)),
      n => n);

    var visits = new List<Visit>();
    while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
      visits.Add(new Visit(t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
    t.Dispose();
    return visits;
  }

  private sealed class SyncChildEnumerator : IChildEnumerator<int>
  {
    private readonly int[] _children;
    private int _i;
    public SyncChildEnumerator(int[] children) => _children = children;

    public bool MoveNext(out NodeAndSiblingIndex<int> child)
    {
      if (_i < _children.Length)
      {
        child = new NodeAndSiblingIndex<int>(_children[_i], _i);
        _i++;
        return true;
      }
      child = default;
      return false;
    }

    public void Dispose() { }
  }

  // --- Async engine over the SAME tree, with genuinely-suspending pulls. ---

  private static async Task<List<Visit>> RunAsync()
  {
    var t = new AsyncDepthFirstTreenumerator<int, int, AsyncChildEnumerator>(
      AsyncRoots(),
      nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)),
      n => n);

    var visits = new List<Visit>();
    await using (t.ConfigureAwait(false))
      while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll))
        visits.Add(new Visit(t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
    return visits;
  }

  private static async Task<List<Visit>> RunAsyncSelect()
  {
    IAsyncTreenumerable<int> source = new AsyncDepthFirstTreenumerable<int, int, AsyncChildEnumerator>(
      AsyncRoots,
      nc => new AsyncChildEnumerator(ChildrenOf(nc.Node)),
      n => n);

    var mapped = source.Select(n => n * 10);
    var t = mapped.GetAsyncDepthFirstTreenumerator();

    var visits = new List<Visit>();
    await using (t.ConfigureAwait(false))
      while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll))
        visits.Add(new Visit(t.Mode, t.Node, t.VisitCount, t.Position.Depth, t.Position.SiblingIndex));
    return visits;
  }

  private static async IAsyncEnumerable<int> AsyncRoots()
  {
    foreach (var r in Roots)
    {
      await Task.Yield(); // force real asynchrony on the root seam
      yield return r;
    }
  }

  private sealed class AsyncChildEnumerator : IAsyncChildEnumerator<int>
  {
    private readonly int[] _children;
    private int _i;
    public AsyncChildEnumerator(int[] children) => _children = children;

    public NodeAndSiblingIndex<int> Current { get; private set; }

    public async ValueTask<bool> MoveNextAsync()
    {
      await Task.Yield(); // force real asynchrony on the child seam
      if (_i < _children.Length)
      {
        Current = new NodeAndSiblingIndex<int>(_children[_i], _i);
        _i++;
        return true;
      }
      return false;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
  }
}
