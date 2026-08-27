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
    /// <summary>Reads preorder-grammar text (<c>"a(b(d,e),c)"</c>) from a forward-only
    /// reader as a depth-first-only async tree, mapping each serialized value through
    /// <paramref name="map"/>. Bounded memory; I/O happens during enumeration. The token
    /// binds per deserialize call and is observed at the reader-refill seam. Each
    /// enumeration opens a fresh reader from the factory and disposes it with the
    /// traversal.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> DeserializeDepthFirstTreeAsync<TNode>(
      Func<TextReader> readerFactory,
      Func<string, TNode> map,
      CancellationToken cancellationToken = default)
      => new AsyncPreorderStreamTreenumerable<TNode, AsyncPreorderTextStream<TNode>>(
        () => new AsyncPreorderTextStream<TNode>(readerFactory(), map, cancellationToken));

    /// <summary>Reads preorder-grammar text (<c>"a(b(d,e),c)"</c>) from a forward-only
    /// reader as a depth-first-only async tree of its raw string values. Bounded memory;
    /// each enumeration opens a fresh reader from the factory and disposes it with the
    /// traversal.</summary>
    public static IAsyncDepthFirstTreenumerable<string> DeserializeDepthFirstTreeAsync(
      Func<TextReader> readerFactory,
      CancellationToken cancellationToken = default)
      => DeserializeDepthFirstTreeAsync(readerFactory, value => value, cancellationToken);

    /// <summary>Reads a preorder-grammar file (<c>"a(b(d,e),c)"</c>) as a depth-first-only
    /// async tree, mapping each serialized value through <paramref name="map"/>. The file is
    /// opened for true asynchronous I/O; each enumeration reopens it.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> DeserializeDepthFirstTreeFromFileAsync<TNode>(
      string path,
      Func<string, TNode> map,
      CancellationToken cancellationToken = default)
      => DeserializeDepthFirstTreeAsync(() => OpenAsyncText(path), map, cancellationToken);

    /// <summary>Reads a preorder-grammar file (<c>"a(b(d,e),c)"</c>) as a depth-first-only
    /// async tree of its raw string values. The file is opened for true asynchronous I/O;
    /// each enumeration reopens it.</summary>
    public static IAsyncDepthFirstTreenumerable<string> DeserializeDepthFirstTreeFromFileAsync(
      string path,
      CancellationToken cancellationToken = default)
      => DeserializeDepthFirstTreeFromFileAsync(path, value => value, cancellationToken);

    /// <summary>Reads level-order-grammar text (<c>"a;b,c;d,e"</c>) from a forward-only
    /// reader as a breadth-first-only async tree, mapping each serialized value through
    /// <paramref name="map"/>. Bounded memory; I/O happens during enumeration. The token
    /// binds per deserialize call and is observed at the reader-refill seam. Each
    /// enumeration opens a fresh reader from the factory and disposes it with the
    /// traversal.</summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> DeserializeBreadthFirstTreeAsync<TNode>(
      Func<TextReader> readerFactory,
      Func<string, TNode> map,
      CancellationToken cancellationToken = default)
      => new AsyncLevelOrderStreamTreenumerable<TNode, AsyncLevelOrderTextStream<TNode>>(
        () => new AsyncLevelOrderTextStream<TNode>(readerFactory(), map, cancellationToken));

    /// <summary>Reads level-order-grammar text (<c>"a;b,c;d,e"</c>) from a forward-only
    /// reader as a breadth-first-only async tree of its raw string values. Bounded memory;
    /// each enumeration opens a fresh reader from the factory and disposes it with the
    /// traversal.</summary>
    public static IAsyncBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeAsync(
      Func<TextReader> readerFactory,
      CancellationToken cancellationToken = default)
      => DeserializeBreadthFirstTreeAsync(readerFactory, value => value, cancellationToken);

    /// <summary>Reads a level-order-grammar file (<c>"a;b,c;d,e"</c>) as a breadth-first-only
    /// async tree, mapping each serialized value through <paramref name="map"/>. The file is
    /// opened for true asynchronous I/O; each enumeration reopens it.</summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> DeserializeBreadthFirstTreeFromFileAsync<TNode>(
      string path,
      Func<string, TNode> map,
      CancellationToken cancellationToken = default)
      => DeserializeBreadthFirstTreeAsync(() => OpenAsyncText(path), map, cancellationToken);

    /// <summary>Reads a level-order-grammar file (<c>"a;b,c;d,e"</c>) as a breadth-first-only
    /// async tree of its raw string values. The file is opened for true asynchronous I/O;
    /// each enumeration reopens it.</summary>
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
