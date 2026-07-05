using Copse.Core;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static string ToFormattedString<TNode>(this IDepthFirstTreenumerable<TNode> source)
    {
      return source.ToFormattedString(node => node.ToString(), 0);
    }

    public static string ToFormattedString<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      int paddingSize)
    {
      return source.ToFormattedString(node => node.ToString(), paddingSize);
    }

    public static string ToFormattedString<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<TNode, string> stringFormatter,
      int paddingSize)
    {
      return string.Join(Environment.NewLine, source.ToFormattedLines(stringFormatter, paddingSize));
    }
  }
}
