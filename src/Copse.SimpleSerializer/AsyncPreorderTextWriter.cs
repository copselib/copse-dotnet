using Copse.Core;
using Copse.Core.Async;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  // The dft payload writer: the terse paren grammar ("a(b(d,e),c)"), emitted from a single
  // depth-first pass -- each node on its first visit, paren/comma structure derived from depth
  // deltas. Values ride ValueToken (CSV-style minimal quoting), so any string is a legal node
  // value. PreorderTextStream is this grammar's reader; change one, check the other.
  //
  // I/O happens at BLOCK granularity: structure and values append synchronously into the shared
  // TextBlockBuffer, and one awaited WriteAsync drains it whenever it reaches the threshold
  // (plus a final drain) -- never a writer call per character or per token. This is the single
  // source of truth: strip the awaits and it collapses to the synchronous PreorderTextWriter
  // (the checked-in .g.cs twin), block-buffered the same way.
  internal static class AsyncPreorderTextWriter
  {
    public static async ValueTask WritePayloadAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> treenumerable,
      TextWriter writer,
      Func<TNode, string> map,
      CancellationToken cancellationToken)
    {
      var treenumerator = treenumerable.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        var buffer = new TextBlockBuffer();
        int previousDepth = -1;

        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();

          if (treenumerator.VisitCount != 1)
            continue;

          var depth = treenumerator.Position.Depth;

          if (previousDepth != -1)
          {
            if (depth > previousDepth)
            {
              buffer.Append('(');
            }
            else
            {
              for (int i = 0; i < previousDepth - depth; i++)
                buffer.Append(')');

              buffer.Append(',');
            }
          }

          ValueToken.AppendTo(buffer, map(treenumerator.Node));
          previousDepth = depth;

          if (buffer.Count >= TextBlockBuffer.DrainThreshold)
          {
            await writer.WriteAsync(buffer.Chars, 0, buffer.Count).ConfigureAwait(false);
            buffer.Clear();
          }
        }

        while (previousDepth-- > 0)
          buffer.Append(')');

        if (buffer.Count > 0)
          await writer.WriteAsync(buffer.Chars, 0, buffer.Count).ConfigureAwait(false);
      }
    }
  }
}
