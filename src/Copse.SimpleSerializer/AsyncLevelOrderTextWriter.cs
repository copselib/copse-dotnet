using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  // The bft payload writer: level-order child groups ("a;b,c;d,e"), O(1) state -- a node's
  // VALUE is emitted when it is scheduled (inside its parent's family) and its family
  // terminator when it is first visited ('|' within a level, ';' at a level boundary): exactly
  // the schedule/visit split serialized (the BreadthFirstTreeTokenizer tokenization rendered to
  // text). Separators buffer until the next value; at end of stream they drop, eliding all
  // trailing empty families. Values ride ValueToken (CSV-style minimal quoting), so any string
  // is a legal node value. LevelOrderTextStream is this grammar's reader; change one, check the
  // other.
  //
  // I/O happens at BLOCK granularity: structure and values append synchronously into the shared
  // TextBlockBuffer, and one awaited WriteAsync drains it whenever it reaches the threshold
  // (plus a final drain) -- never a writer call per character or per token. This is the single
  // source of truth: strip the awaits and it collapses to the synchronous LevelOrderTextWriter
  // (the checked-in .g.cs twin), block-buffered the same way.
  internal static class AsyncLevelOrderTextWriter
  {
    public static async ValueTask WritePayloadAsync<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> treenumerable,
      TextWriter writer,
      Func<TNode, string> map)
    {
      var treenumerator = treenumerable.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        var buffer = new TextBlockBuffer();
        var pendingSeparators = new Queue<char>();
        var currentLevelDepth = -1;
        var valueOpenInFamily = false;

        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            if (pendingSeparators.Count > 0)
            {
              while (pendingSeparators.Count > 0)
                buffer.Append(pendingSeparators.Dequeue());

              valueOpenInFamily = false;
            }

            if (valueOpenInFamily)
              buffer.Append(',');

            ValueToken.AppendTo(buffer, map(treenumerator.Node));
            valueOpenInFamily = true;

            if (buffer.Count >= TextBlockBuffer.DrainThreshold)
            {
              await writer.WriteAsync(buffer.Chars, 0, buffer.Count).ConfigureAwait(false);
              buffer.Clear();
            }
          }
          else if (treenumerator.VisitCount == 1)
          {
            if (treenumerator.Position.Depth == currentLevelDepth)
            {
              pendingSeparators.Enqueue('|');
            }
            else
            {
              pendingSeparators.Enqueue(';');
              currentLevelDepth++;
            }
          }
        }

        if (buffer.Count > 0)
          await writer.WriteAsync(buffer.Chars, 0, buffer.Count).ConfigureAwait(false);
      }
    }
  }
}
