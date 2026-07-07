using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using Copse.Linq.TreeTokenizer.DepthFirstTree;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree rendered as box-drawing lines (one per node), as a lazy async sequence.</summary>
    public static IAsyncEnumerable<string> ToFormattedLines<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
      => source.ToFormattedLines(node => node.ToString(), 0);

    /// <summary>The tree rendered as box-drawing lines with the given branch padding width.</summary>
    public static IAsyncEnumerable<string> ToFormattedLines<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, int paddingSize)
      => source.ToFormattedLines(node => node.ToString(), paddingSize);

    /// <summary>The tree rendered as box-drawing lines with a custom node formatter and branch padding.</summary>
    public static async IAsyncEnumerable<string> ToFormattedLines<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, string> stringFormatter,
      int paddingSize)
    {
      // The renderer walks the token stream in reverse (deepest-last), which requires the whole
      // stream in hand -- so drain the async tokenizer first, then do the (synchronous) drawing.
      var tokens = new List<DepthFirstTreeToken<TNode>>();
      await foreach (var token in source.ToDepthFirstTreeTokenizer().ConfigureAwait(false))
        tokens.Add(token);

      const char BAR_NODE = '│';
      const char INTERIOR_BRANCH_NODE = '├';
      const char EXTERIOR_BRANCH_NODE = '└';
      const char WHITESPACE_NODE = ' ';
      const char BRANCH_PADDING = '─';

      var branchPadding = new string(BRANCH_PADDING, paddingSize);
      var whitespacePadding = new string(WHITESPACE_NODE, paddingSize);
      var nodes = new List<char>();
      var results = new Stack<string>();
      var builder = new StringBuilder();

      for (var tokenIndex = tokens.Count - 1; tokenIndex >= 0; tokenIndex--)
      {
        var token = tokens[tokenIndex];
        switch (token.Type)
        {
          case DepthFirstTreeTokenType.EndChildGroup:
            if (nodes.Count > 0 && (nodes[nodes.Count - 1] == INTERIOR_BRANCH_NODE || nodes[nodes.Count - 1] == EXTERIOR_BRANCH_NODE))
              nodes.ReplaceLast(BAR_NODE);
            nodes.Add(WHITESPACE_NODE);
            break;

          case DepthFirstTreeTokenType.StartChildGroup:
            builder.Remove(builder.Length - (paddingSize + 1), paddingSize + 1);
            nodes.RemoveLast();
            break;

          default:
            if (nodes.Count == 0)
            {
              results.Push(stringFormatter(token.Node));
              continue;
            }

            var node = nodes[nodes.Count - 1];
            if (node == WHITESPACE_NODE)
              nodes.ReplaceLast(EXTERIOR_BRANCH_NODE);
            else if (node == EXTERIOR_BRANCH_NODE || node == BAR_NODE)
              nodes.ReplaceLast(INTERIOR_BRANCH_NODE);

            builder.Clear();
            for (var i = 0; i < nodes.Count; i++)
            {
              node = nodes[i];
              builder.Append(node);
              if (node == BAR_NODE || node == WHITESPACE_NODE)
                builder.Append(whitespacePadding);
              else
                builder.Append(branchPadding);
            }
            builder.Append(stringFormatter(token.Node));
            results.Push(builder.ToString());
            break;
        }
      }

      while (results.Count > 0)
        yield return results.Pop();
    }
  }
}
