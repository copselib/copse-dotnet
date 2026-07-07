using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree rendered as a single box-drawing string (lines joined by the platform newline).</summary>
    public static ValueTask<string> ToFormattedStringAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
      => source.ToFormattedStringAsync(node => node.ToString(), 0);

    /// <summary>The tree rendered as a single box-drawing string with the given branch padding width.</summary>
    public static ValueTask<string> ToFormattedStringAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, int paddingSize)
      => source.ToFormattedStringAsync(node => node.ToString(), paddingSize);

    /// <summary>The tree rendered as a single box-drawing string with a custom node formatter and branch padding.</summary>
    public static async ValueTask<string> ToFormattedStringAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, string> stringFormatter,
      int paddingSize)
    {
      var lines = new List<string>();
      await foreach (var line in source.ToFormattedLines(stringFormatter, paddingSize).ConfigureAwait(false))
        lines.Add(line);

      return string.Join(Environment.NewLine, lines);
    }
  }
}
