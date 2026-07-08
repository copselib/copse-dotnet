using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Core.Async;
using System;
using System.IO;
using System.Threading;

namespace Copse.SimpleSerializer
{
  // The ASYNC stream tier: the async analog of TreeSerializer.Stream.cs. A forward-only reader is
  // bounded memory and single-pass, so an async deserialize yields the NARROW async interface
  // (IAsyncDepthFirstTreenumerable / IAsyncBreadthFirstTreenumerable); the I/O happens during
  // enumeration (each MoveNextAsync awaits the reader), not in these deferred factories. The FromFile
  // overloads open the file for asynchronous I/O (FileStream useAsync + StreamReader.ReadAsync) --
  // the intended payoff.
  //
  // The CancellationToken binds per DESERIALIZE CALL and covers every traversal the result serves:
  // cancellation is observed cooperatively at the scanner's block-refill seam (once per 4096
  // characters of I/O -- see AsyncValueTokenStreamScanner). The token deliberately does NOT ride
  // the treenumerator contract (MoveNextAsync/DisposeAsync are token-free, like
  // IAsyncEnumerator<T>); it enters here, at the I/O edge, where there is real latency to interrupt.
  public static partial class TreeSerializer
  {
    public static IAsyncDepthFirstTreenumerable<TValue> DeserializeDepthFirstTreeAsync<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map,
      CancellationToken cancellationToken = default)
      => new AsyncPreorderStreamTreenumerable<TValue, AsyncPreorderTextStream<TValue>>(
        () => new AsyncPreorderTextStream<TValue>(readerFactory(), map, cancellationToken));

    public static IAsyncDepthFirstTreenumerable<string> DeserializeDepthFirstTreeAsync(
      Func<TextReader> readerFactory,
      CancellationToken cancellationToken = default)
      => DeserializeDepthFirstTreeAsync(readerFactory, value => value, cancellationToken);

    public static IAsyncDepthFirstTreenumerable<TValue> DeserializeDepthFirstTreeFromFileAsync<TValue>(
      string path,
      Func<string, TValue> map,
      CancellationToken cancellationToken = default)
      => DeserializeDepthFirstTreeAsync(() => OpenAsyncText(path), map, cancellationToken);

    public static IAsyncDepthFirstTreenumerable<string> DeserializeDepthFirstTreeFromFileAsync(
      string path,
      CancellationToken cancellationToken = default)
      => DeserializeDepthFirstTreeFromFileAsync(path, value => value, cancellationToken);

    public static IAsyncBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstTreeAsync<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map,
      CancellationToken cancellationToken = default)
      => new AsyncLevelOrderStreamTreenumerable<TValue, AsyncLevelOrderTextStream<TValue>>(
        () => new AsyncLevelOrderTextStream<TValue>(readerFactory(), map, cancellationToken));

    public static IAsyncBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeAsync(
      Func<TextReader> readerFactory,
      CancellationToken cancellationToken = default)
      => DeserializeBreadthFirstTreeAsync(readerFactory, value => value, cancellationToken);

    public static IAsyncBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstTreeFromFileAsync<TValue>(
      string path,
      Func<string, TValue> map,
      CancellationToken cancellationToken = default)
      => DeserializeBreadthFirstTreeAsync(() => OpenAsyncText(path), map, cancellationToken);

    public static IAsyncBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeFromFileAsync(
      string path,
      CancellationToken cancellationToken = default)
      => DeserializeBreadthFirstTreeFromFileAsync(path, value => value, cancellationToken);

    // A reader over a file opened for asynchronous I/O (so StreamReader.ReadAsync is a real async
    // read, not a sync read wrapped in a completed task).
    private static TextReader OpenAsyncText(string path)
      => new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true));
  }
}
