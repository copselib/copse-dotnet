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
    public static IReadOnlyList<string> ToFormattedLines<TNode, TEdge>(this IDagnumerable<TNode, TEdge> source)
      => source.ToFormattedLines(node => node?.ToString(), edge => edge?.ToString());

    /// <summary>The dag rendered as box-drawing lines with custom node and edge formatters (an empty or null edge text omits the payload segment).</summary>
    public static IReadOnlyList<string> ToFormattedLines<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, string> nodeFormatter,
      Func<TEdge, string> edgeFormatter)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (nodeFormatter == null)
        throw new ArgumentNullException(nameof(nodeFormatter));
      if (edgeFormatter == null)
        throw new ArgumentNullException(nameof(edgeFormatter));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;
      var (inOffsets, _, _) = structure.InAdjacency();
      var lines = new List<string>(buffer.Count);
      var expandedOrdinals = new HashSet<int>();
      var builder = new StringBuilder();

      // Explicit stack (no recursion -- depth is the dag's, not ours); children pushed in
      // reverse so out-edge order pops first. Sources are edge-less root frames, in ordinal
      // order (in-degree zero in the buffer's in-adjacency).
      var frames = new Stack<(int Ordinal, TEdge Edge, bool HasEdge, string Prefix, bool IsLast)>();

      for (var ordinal = buffer.Count - 1; ordinal >= 0; ordinal--)
        if (inOffsets[ordinal + 1] == inOffsets[ordinal])
          frames.Push((ordinal, default, false, string.Empty, true));

      while (frames.Count > 0)
      {
        var frame = frames.Pop();

        var isShared = inOffsets[frame.Ordinal + 1] - inOffsets[frame.Ordinal] >= 2;
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

        builder.Append(nodeFormatter(buffer[frame.Ordinal]));

        if (isShared)
        {
          builder.Append(" #");
          builder.Append(buffer.SourceOrdinal(frame.Ordinal));
        }

        if (!isFirstEncounter)
          builder.Append(" ↺");

        lines.Add(builder.ToString());

        var firstSlot = outOffsets[frame.Ordinal];
        var endSlot = outOffsets[frame.Ordinal + 1];

        if (!isFirstEncounter || firstSlot == endSlot)
          continue;

        var childPrefix = frame.HasEdge
          ? frame.Prefix + (frame.IsLast ? "   " : "│  ")
          : frame.Prefix;

        for (var slot = endSlot - 1; slot >= firstSlot; slot--)
          frames.Push((outTargets[slot], outPayloads[slot], true, childPrefix, slot == endSlot - 1));
      }

      return lines;
    }

    /// <summary>
    /// <see cref="ToFormattedLines{TNode, TEdge}(IDagnumerable{TNode, TEdge})"/> joined
    /// into one newline-separated string -- the dump-me shape (LinqPad's <c>Dump()</c>, a
    /// console write, a log line).
    /// </summary>
    public static string ToFormattedString<TNode, TEdge>(this IDagnumerable<TNode, TEdge> source)
      => string.Join(Environment.NewLine, source.ToFormattedLines());

    /// <summary>The joined rendering with custom node and edge formatters.</summary>
    public static string ToFormattedString<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, string> nodeFormatter,
      Func<TEdge, string> edgeFormatter)
      => string.Join(Environment.NewLine, source.ToFormattedLines(nodeFormatter, edgeFormatter));
  }
}
