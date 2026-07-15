using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The tree rendered as box-drawing lines, one per node. Eager, like every <c>To*</c>
    /// terminal: the source is walked once, at the call, and the completed lines are returned —
    /// finite sources only.
    /// </summary>
    public static ValueTask<IReadOnlyList<string>> ToFormattedLinesAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      return source.ToFormattedLinesAsync(node => node.ToString(), 0, cancellationToken);
    }

    /// <summary>The tree rendered as box-drawing lines with the given branch padding width.</summary>
    public static ValueTask<IReadOnlyList<string>> ToFormattedLinesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      int paddingSize,
      CancellationToken cancellationToken = default)
    {
      return source.ToFormattedLinesAsync(node => node.ToString(), paddingSize, cancellationToken);
    }

    /// <summary>The tree rendered as box-drawing lines with a custom node formatter and branch padding.</summary>
    public static async ValueTask<IReadOnlyList<string>> ToFormattedLinesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, string> stringFormatter,
      int paddingSize,
      CancellationToken cancellationToken = default)
    {
      // The one walk of the source: a (text, depth) record per node, formatter evaluated here so
      // it runs exactly once per node in preorder. Depth deltas carry the full tree shape (a step
      // of +1 opens a child group, a drop of k closes k), so nothing else needs to be kept.
      var lines = new List<(string Text, int Depth)>();

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();

          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          lines.Add((stringFormatter(treenumerator.Node), treenumerator.Position.Depth));
        }
      }

      return RenderFormattedLines(lines, paddingSize);
    }

    // The branch glyph on a node's own line (├ vs └) depends on whether a later sibling follows,
    // so rendering runs bottom-up: walking the records in reverse, the last sibling of each group
    // is seen first. Each rendered line lands at its own index in the pre-sized result — the
    // records are the single working buffer.
    private static IReadOnlyList<string> RenderFormattedLines(List<(string Text, int Depth)> lines, int paddingSize)
    {
      const char BAR_NODE = '│';
      const char INTERIOR_BRANCH_NODE = '├';
      const char EXTERIOR_BRANCH_NODE = '└';
      const char WHITESPACE_NODE = ' ';
      const char BRANCH_PADDING = '─';

      var branchPadding = new string(BRANCH_PADDING, paddingSize);
      var whitespacePadding = new string(WHITESPACE_NODE, paddingSize);

      var renderedLines = new string[lines.Count];

      // One glyph per ancestor level of the line being rendered: WHITESPACE below a group's last
      // child, BAR below its earlier children, a branch glyph only ever at the deepest level.
      var columns = new List<char>();

      var builder = new StringBuilder();

      // The record after the final line (walking backward, the one "before" it) is the forest
      // root baseline: the forward stream's tail closed every open group back to depth zero.
      var previousDepth = 0;

      for (var lineIndex = lines.Count - 1; lineIndex >= 0; lineIndex--)
      {
        var (text, depth) = lines[lineIndex];

        if (depth >= previousDepth)
        {
          // The forward stream closed (depth - previousDepth) child groups between these lines;
          // walking backward, each close re-opens a column. A group seen for the first time from
          // below starts at WHITESPACE (its last child renders └); the shallower column's branch
          // glyph decays to BAR — an earlier sibling's line above still owes it a column.
          for (var reopenedGroups = depth - previousDepth; reopenedGroups > 0; reopenedGroups--)
          {
            var deepestColumn = columns.Count - 1;

            if (deepestColumn >= 0 && (columns[deepestColumn] == INTERIOR_BRANCH_NODE || columns[deepestColumn] == EXTERIOR_BRANCH_NODE))
              columns[deepestColumn] = BAR_NODE;

            columns.Add(WHITESPACE_NODE);
          }
        }
        else
        {
          // Preorder depth rises by exactly one per step, so walking backward it falls by exactly
          // one: the group this line's child opened closes, retiring its column.
          columns.RemoveAt(columns.Count - 1);
        }

        previousDepth = depth;

        if (columns.Count == 0)
        {
          // A root: no ancestors, no prefix.
          renderedLines[lineIndex] = text;
          continue;
        }

        // The deepest column is this line's own branch glyph: the group's last child (a fresh
        // WHITESPACE column) renders └; anything already claimed below renders ├.
        var ownBranchColumn = columns.Count - 1;

        if (columns[ownBranchColumn] == WHITESPACE_NODE)
          columns[ownBranchColumn] = EXTERIOR_BRANCH_NODE;
        else if (columns[ownBranchColumn] == EXTERIOR_BRANCH_NODE || columns[ownBranchColumn] == BAR_NODE)
          columns[ownBranchColumn] = INTERIOR_BRANCH_NODE;

        builder.Clear();

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
          var column = columns[columnIndex];

          builder.Append(column);

          if (column == BAR_NODE || column == WHITESPACE_NODE)
            builder.Append(whitespacePadding);
          else
            builder.Append(branchPadding);
        }

        builder.Append(text);

        renderedLines[lineIndex] = builder.ToString();
      }

      return renderedLines;
    }
  }
}
