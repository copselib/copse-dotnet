using Copse.Core;
using Copse.Linq.Experimental.Treenumerators;
using Copse.Linq.Treenumerables;
using Copse.Treenumerables;
using System;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> Graft<TNode>(
      this ITreenumerable<TNode> source,
      ITreenumerable<TNode> scion)
      => Graft(source, _ => scion, _ => true);

    public static ITreenumerable<TNode> Graft<TNode>(
      this ITreenumerable<TNode> source,
      ITreenumerable<TNode> scion,
      Func<NodeVisit<TNode>, bool> predicate)
      => Graft(source, _ => scion, predicate);

    public static ITreenumerable<TNode> Graft<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeVisit<TNode>, ITreenumerable<TNode>> scionGenerator)
      => Graft(source, scionGenerator, _ => true);

    public static ITreenumerable<TNode> Graft<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeVisit<TNode>, ITreenumerable<TNode>> scionGenerator,
      Func<NodeVisit<TNode>, bool> predicate)
      => Graft(source, visit => visit.Node, scionGenerator, predicate);

    public static ITreenumerable<TResult> Graft<TSource, TResult>(
      this ITreenumerable<TSource> source,
      Func<NodeVisit<TSource>, TResult> selector,
      Func<NodeVisit<TSource>, ITreenumerable<TResult>> scionGenerator,
      Func<NodeVisit<TSource>, bool> predicate)
      => new DelegatingTreenumerable<TResult>(
        () => throw new NotImplementedException(),
        () => new GraftDepthFirstTreenumerator<TSource, TResult>(
          source.GetDepthFirstTreenumerator,
          selector,
          scionGenerator,
          predicate));
  }
}
