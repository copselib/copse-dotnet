using System;
using System.Collections.Generic;
using System.Text;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The dag rendered as box-drawing lines -- the tree family's pretty-print carried to
    /// shared parentage. Depth-first expansion from the sources in out-edge order, edge
    /// payloads on the branch lines (<c>├─ 0.60 → left</c>; omitted when the edge text is
    /// empty). SHARING is the one new problem and ordinals are the answer: a node with two or
    /// more in-edges is tagged <c>#ordinal</c>, expanded in full at its FIRST encounter, and
    /// rendered as a one-line <c>↺</c> reference at every later one -- so every edge appears
    /// exactly once and no subtree is printed twice. Eager, like every <c>To*</c> terminal:
    /// one walk into a capture at the call (documented materialization), finite sources only.
    /// </summary>
    public static IReadOnlyList<string> ToFormattedLines<TNode, TEdge>(this IForwardDagnumerable<TNode, TEdge> source)
      => source.ToFormattedLines(node => node?.ToString(), edge => edge?.ToString());

    /// <summary>The dag rendered as box-drawing lines with custom node and edge formatters (an empty or null edge text omits the payload segment).</summary>
    public static IReadOnlyList<string> ToFormattedLines<TNode, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Func<TNode, string> nodeFormatter,
      Func<TEdge, string> edgeFormatter)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (nodeFormatter == null)
        throw new ArgumentNullException(nameof(nodeFormatter));
      if (edgeFormatter == null)
        throw new ArgumentNullException(nameof(edgeFormatter));

      var capture = DagCapture<TNode, TEdge>.From(source);
      var lines = new List<string>(capture.Entries.Count);
      var expandedOrdinals = new HashSet<int>();
      var builder = new StringBuilder();

      // Explicit stack (no recursion -- depth is the dag's, not ours); children pushed in
      // reverse so out-edge order pops first. Sources are edge-less root frames.
      var frames = new Stack<(int Ordinal, TEdge Edge, bool HasEdge, string Prefix, bool IsLast)>();

      for (var sourceIndex = capture.Sources.Count - 1; sourceIndex >= 0; sourceIndex--)
        frames.Push((capture.Sources[sourceIndex], default, false, string.Empty, true));

      while (frames.Count > 0)
      {
        var frame = frames.Pop();

        var isShared = capture.InEdges.TryGetValue(frame.Ordinal, out var inEdges) && inEdges.Count >= 2;
        var isFirstEncounter = expandedOrdinals.Add(frame.Ordinal);

        builder.Clear();
        builder.Append(frame.Prefix);

        if (frame.HasEdge)
        {
          builder.Append(frame.IsLast ? "└─ " : "├─ ");

          var edgeText = edgeFormatter(frame.Edge);

          if (!string.IsNullOrEmpty(edgeText))
          {
            builder.Append(edgeText);
            builder.Append(" → ");
          }
        }

        builder.Append(nodeFormatter(capture.Values[frame.Ordinal]));

        if (isShared)
        {
          builder.Append(" #");
          builder.Append(frame.Ordinal);
        }

        if (!isFirstEncounter)
          builder.Append(" ↺");

        lines.Add(builder.ToString());

        if (!isFirstEncounter || !capture.OutEdges.TryGetValue(frame.Ordinal, out var outEdges))
          continue;

        var childPrefix = frame.HasEdge
          ? frame.Prefix + (frame.IsLast ? "   " : "│  ")
          : frame.Prefix;

        for (var outEdgeIndex = outEdges.Count - 1; outEdgeIndex >= 0; outEdgeIndex--)
        {
          var (childOrdinal, edge) = outEdges[outEdgeIndex];
          frames.Push((childOrdinal, edge, true, childPrefix, outEdgeIndex == outEdges.Count - 1));
        }
      }

      return lines;
    }

    /// <summary>
    /// <see cref="ToFormattedLines{TNode, TEdge}(IForwardDagnumerable{TNode, TEdge})"/> joined
    /// into one newline-separated string -- the dump-me shape (LinqPad's <c>Dump()</c>, a
    /// console write, a log line).
    /// </summary>
    public static string ToFormattedString<TNode, TEdge>(this IForwardDagnumerable<TNode, TEdge> source)
      => string.Join(Environment.NewLine, source.ToFormattedLines());

    /// <summary>The joined rendering with custom node and edge formatters.</summary>
    public static string ToFormattedString<TNode, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Func<TNode, string> nodeFormatter,
      Func<TEdge, string> edgeFormatter)
      => string.Join(Environment.NewLine, source.ToFormattedLines(nodeFormatter, edgeFormatter));
  }
}
