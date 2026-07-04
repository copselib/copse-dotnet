using Copse.Core;
using Copse.Linq.Treenumerators;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this ITreenumerable<TLeft> leftTreenumerable,
      ITreenumerable<TRight> rightTreenumerable)
    {
      return
        leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);
    }

    public static IDepthFirstTreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this IDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IDepthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);

    public static IBreadthFirstTreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this IBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);
  }
}
