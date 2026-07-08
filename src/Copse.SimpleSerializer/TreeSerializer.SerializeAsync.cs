using Copse.Core.Async;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  public static partial class TreeSerializer
  {
    // The async serialize surface: awaited writes over a forward-only TextWriter, receivers on
    // the ASYNC narrow contracts -- the only road to text for a tree whose pulls suspend (an
    // async memo, an async-deserialized stream). Awaitable -> carries the Async suffix. This is
    // the codegen source of truth for the sync Serialize surface (TreeSerializer.Serialize.g.cs).
    public static ValueTask SerializeDepthFirstTreeAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map)
      => AsyncPreorderTextWriter.WritePayloadAsync(treenumerable, writer, map);

    public static ValueTask SerializeDepthFirstTreeAsync(this IAsyncDepthFirstTreenumerable<string> treenumerable, TextWriter writer)
      => treenumerable.SerializeDepthFirstTreeAsync(writer, node => node);

    public static ValueTask SerializeBreadthFirstTreeAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map)
      => AsyncLevelOrderTextWriter.WritePayloadAsync(treenumerable, writer, map);

    public static ValueTask SerializeBreadthFirstTreeAsync(this IAsyncBreadthFirstTreenumerable<string> treenumerable, TextWriter writer)
      => treenumerable.SerializeBreadthFirstTreeAsync(writer, node => node);
  }
}
