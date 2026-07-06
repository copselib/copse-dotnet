using Copse.Core;
using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // The dft payload writer: the terse paren grammar ("a(b(d,e),c)"), emitted from a single
  // depth-first pass -- each node on its first visit, paren/comma structure derived from depth
  // deltas. Values ride ValueToken (CSV-style minimal quoting), so any string is a legal node
  // value. PreorderTextStream is this grammar's reader; change one, check the other.
  internal static class PreorderTextWriter
  {
    public static void WritePayload<TNode>(
      IDepthFirstTreenumerable<TNode> treenumerable,
      TextWriter writer,
      Func<TNode, string> map)
    {
      using (var treenumerator = treenumerable.GetDepthFirstTreenumerator())
      {
        int previousDepth = -1;

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.VisitCount != 1)
            continue;

          var depth = treenumerator.Position.Depth;

          if (previousDepth != -1)
          {
            if (depth > previousDepth)
            {
              writer.Write('(');
            }
            else
            {
              for (int i = 0; i < previousDepth - depth; i++)
                writer.Write(')');

              writer.Write(',');
            }
          }

          ValueToken.Write(writer, map(treenumerator.Node));

          previousDepth = depth;
        }

        while (previousDepth-- > 0)
          writer.Write(')');
      }
    }
  }
}
