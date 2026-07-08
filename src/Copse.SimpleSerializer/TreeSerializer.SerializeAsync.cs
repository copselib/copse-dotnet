using Copse.Core.Async;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  public static partial class TreeSerializer
  {
    // The async serialize surface: awaited writes over a forward-only TextWriter, receivers on
    // the ASYNC narrow contracts -- the only road to text for a tree whose pulls suspend (an
    // async memo, an async-deserialized stream). Awaitable -> carries the Async suffix. This is
    // the codegen source of truth for the sync Serialize surface (TreeSerializer.Serialize.g.cs);
    // the CancellationToken (checked once per emitted visit in the writers) is elided from it.
    public static ValueTask SerializeDepthFirstTreeAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map, CancellationToken cancellationToken = default)
      => AsyncPreorderTextWriter.WritePayloadAsync(treenumerable, writer, map, cancellationToken);

    public static ValueTask SerializeDepthFirstTreeAsync(this IAsyncDepthFirstTreenumerable<string> treenumerable, TextWriter writer, CancellationToken cancellationToken = default)
      => treenumerable.SerializeDepthFirstTreeAsync(writer, node => node, cancellationToken);

    public static ValueTask SerializeBreadthFirstTreeAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map, CancellationToken cancellationToken = default)
      => AsyncLevelOrderTextWriter.WritePayloadAsync(treenumerable, writer, map, cancellationToken);

    public static ValueTask SerializeBreadthFirstTreeAsync(this IAsyncBreadthFirstTreenumerable<string> treenumerable, TextWriter writer, CancellationToken cancellationToken = default)
      => treenumerable.SerializeBreadthFirstTreeAsync(writer, node => node, cancellationToken);
  }
}
