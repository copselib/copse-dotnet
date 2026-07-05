using Copse.Core;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> Hide<TNode>(
      this ITreenumerable<TNode> source)
    {
      return new HideTreenumerable<TNode>(source);
    }

    public static IDepthFirstTreenumerable<TNode> Hide<TNode>(
      this IDepthFirstTreenumerable<TNode> source)
      => TreenumerableFactory.CreateDepthFirst(
        () => new Treenumerators.HideTreenumerator<TNode>(source.GetDepthFirstTreenumerator));

    public static IBreadthFirstTreenumerable<TNode> Hide<TNode>(
      this IBreadthFirstTreenumerable<TNode> source)
      => TreenumerableFactory.CreateBreadthFirst(
        () => new Treenumerators.HideTreenumerator<TNode>(source.GetBreadthFirstTreenumerator));
  }
}
