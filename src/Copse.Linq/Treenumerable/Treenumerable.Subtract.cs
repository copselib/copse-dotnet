using Copse.Core;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TLeft> Subtract<TLeft, TRight>(
      this ITreenumerable<TLeft> leftTreenumerable,
      ITreenumerable<TRight> rightTreenumerable)
    {
      return
        leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasRight)
        .Select(mergeNodeContext => mergeNodeContext.Node.Left);
    }

    public static IDepthFirstTreenumerable<TLeft> Subtract<TLeft, TRight>(
      this IDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IDepthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasRight)
        .Select(mergeNodeContext => mergeNodeContext.Node.Left);

    public static IBreadthFirstTreenumerable<TLeft> Subtract<TLeft, TRight>(
      this IBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasRight)
        .Select(mergeNodeContext => mergeNodeContext.Node.Left);
  }
}
