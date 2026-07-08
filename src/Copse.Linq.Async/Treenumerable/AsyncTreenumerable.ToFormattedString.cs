using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree rendered as a single box-drawing string (lines joined by the platform newline).</summary>
    public static ValueTask<string> ToFormattedStringAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      return source.ToFormattedStringAsync(node => node.ToString(), 0, cancellationToken);
    }

    /// <summary>The tree rendered as a single box-drawing string with the given branch padding width.</summary>
    public static ValueTask<string> ToFormattedStringAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      int paddingSize,
      CancellationToken cancellationToken = default)
    {
      return source.ToFormattedStringAsync(node => node.ToString(), paddingSize, cancellationToken);
    }

    /// <summary>The tree rendered as a single box-drawing string with a custom node formatter and branch padding.</summary>
    public static async ValueTask<string> ToFormattedStringAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, string> stringFormatter,
      int paddingSize,
      CancellationToken cancellationToken = default)
    {
      var lines = new List<string>();
      await foreach (var line in source.ToFormattedLines(stringFormatter, paddingSize).ConfigureAwait(false))
      {
        cancellationToken.ThrowIfCancellationRequested();
        lines.Add(line);
      }

      return string.Join(Environment.NewLine, lines);
    }
  }
}
