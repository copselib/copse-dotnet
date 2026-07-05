using Copse.Core;
using Copse.Linq.Treenumerators;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this ITreenumerable<TLeft> leftTreenumerable,
      ITreenumerable<TRight> rightTreenumerable)
      => TreenumerableFactory.Create(
        () => new StructuralMergeBreadthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetBreadthFirstTreenumerator,
          rightTreenumerable.GetBreadthFirstTreenumerator),
        () => new StructuralMergeDepthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetDepthFirstTreenumerator,
          rightTreenumerable.GetDepthFirstTreenumerator));

    public static IDepthFirstTreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this IDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IDepthFirstTreenumerable<TRight> rightTreenumerable)
      => TreenumerableFactory.CreateDepthFirst(
        () => new StructuralMergeDepthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetDepthFirstTreenumerator,
          rightTreenumerable.GetDepthFirstTreenumerator));

    public static IBreadthFirstTreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this IBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => TreenumerableFactory.CreateBreadthFirst(
        () => new StructuralMergeBreadthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetBreadthFirstTreenumerator,
          rightTreenumerable.GetBreadthFirstTreenumerator));
  }
}
