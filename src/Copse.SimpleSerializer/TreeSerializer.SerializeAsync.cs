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
    /// <summary>Writes the tree to <paramref name="writer"/> in the preorder grammar
    /// (<c>"a(b(d,e),c)"</c>), mapping each node through <paramref name="map"/> to its
    /// serialized value. Cancellation is observed once per emitted visit.</summary>
    public static ValueTask SerializeDepthFirstTreeAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map, CancellationToken cancellationToken = default)
      => AsyncPreorderTextWriter.WritePayloadAsync(treenumerable, writer, map, cancellationToken);

    /// <summary>Writes the tree to <paramref name="writer"/> in the preorder grammar
    /// (<c>"a(b(d,e),c)"</c>), taking each node's string as its serialized value.
    /// Cancellation is observed once per emitted visit.</summary>
    public static ValueTask SerializeDepthFirstTreeAsync(this IAsyncDepthFirstTreenumerable<string> treenumerable, TextWriter writer, CancellationToken cancellationToken = default)
      => treenumerable.SerializeDepthFirstTreeAsync(writer, node => node, cancellationToken);

    /// <summary>Writes the tree to <paramref name="writer"/> in the level-order grammar
    /// (<c>"a;b,c;d,e"</c>), mapping each node through <paramref name="map"/> to its
    /// serialized value. Cancellation is observed once per emitted visit.</summary>
    public static ValueTask SerializeBreadthFirstTreeAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map, CancellationToken cancellationToken = default)
      => AsyncLevelOrderTextWriter.WritePayloadAsync(treenumerable, writer, map, cancellationToken);

    /// <summary>Writes the tree to <paramref name="writer"/> in the level-order grammar
    /// (<c>"a;b,c;d,e"</c>), taking each node's string as its serialized value.
    /// Cancellation is observed once per emitted visit.</summary>
    public static ValueTask SerializeBreadthFirstTreeAsync(this IAsyncBreadthFirstTreenumerable<string> treenumerable, TextWriter writer, CancellationToken cancellationToken = default)
      => treenumerable.SerializeBreadthFirstTreeAsync(writer, node => node, cancellationToken);
  }
}
