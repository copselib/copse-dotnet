using Copse.Core;
using System;
using System.Collections.Generic;
using System.IO;

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
  internal static class LevelOrderTextWriter
  {
    public static void WritePayload<TNode>(
      IBreadthFirstTreenumerable<TNode> treenumerable,
      TextWriter writer,
      Func<TNode, string> map)
    {
      using (var treenumerator = treenumerable.GetBreadthFirstTreenumerator())
      {
        var pendingSeparators = new Queue<char>();
        var currentLevelDepth = -1;
        var valueOpenInFamily = false;

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            if (pendingSeparators.Count > 0)
            {
              while (pendingSeparators.Count > 0)
                writer.Write(pendingSeparators.Dequeue());

              valueOpenInFamily = false;
            }

            if (valueOpenInFamily)
              writer.Write(',');

            ValueToken.Write(writer, map(treenumerator.Node));

            valueOpenInFamily = true;
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
      }
    }
  }
}
