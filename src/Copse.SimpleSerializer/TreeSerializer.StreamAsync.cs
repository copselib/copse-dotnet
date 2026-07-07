using Copse.Async;
using Copse.Core.Async;
using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // The ASYNC stream tier: the async analog of TreeSerializer.Stream.cs. A forward-only reader is
  // bounded memory and single-pass, so an async deserialize yields the NARROW async interface
  // (IAsyncDepthFirstTreenumerable / IAsyncBreadthFirstTreenumerable); the I/O happens during
  // enumeration (each MoveNextAsync awaits the reader), not in these deferred factories. The FromFile
  // overloads open the file for asynchronous I/O (FileStream useAsync + StreamReader.ReadAsync) --
  // the intended payoff. net8.0-only (the async contracts and Copse.Async are net8.0).
  public static partial class TreeSerializer
  {
    public static IAsyncDepthFirstTreenumerable<TValue> DeserializeDepthFirstTreeAsync<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new AsyncPreorderStreamTreenumerable<TValue, AsyncPreorderTextStream<TValue>>(
        () => new AsyncPreorderTextStream<TValue>(readerFactory(), map));

    public static IAsyncDepthFirstTreenumerable<string> DeserializeDepthFirstTreeAsync(Func<TextReader> readerFactory)
      => DeserializeDepthFirstTreeAsync(readerFactory, value => value);

    public static IAsyncDepthFirstTreenumerable<TValue> DeserializeDepthFirstTreeFromFileAsync<TValue>(string path, Func<string, TValue> map)
      => DeserializeDepthFirstTreeAsync(() => OpenAsyncText(path), map);

    public static IAsyncDepthFirstTreenumerable<string> DeserializeDepthFirstTreeFromFileAsync(string path)
      => DeserializeDepthFirstTreeFromFileAsync(path, value => value);

    public static IAsyncBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstTreeAsync<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new AsyncLevelOrderStreamTreenumerable<TValue, AsyncLevelOrderTextStream<TValue>>(
        () => new AsyncLevelOrderTextStream<TValue>(readerFactory(), map));

    public static IAsyncBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeAsync(Func<TextReader> readerFactory)
      => DeserializeBreadthFirstTreeAsync(readerFactory, value => value);

    public static IAsyncBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstTreeFromFileAsync<TValue>(string path, Func<string, TValue> map)
      => DeserializeBreadthFirstTreeAsync(() => OpenAsyncText(path), map);

    public static IAsyncBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeFromFileAsync(string path)
      => DeserializeBreadthFirstTreeFromFileAsync(path, value => value);

    // A reader over a file opened for asynchronous I/O (so StreamReader.ReadAsync is a real async
    // read, not a sync read wrapped in a completed task).
    private static TextReader OpenAsyncText(string path)
      => new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true));
  }
}
