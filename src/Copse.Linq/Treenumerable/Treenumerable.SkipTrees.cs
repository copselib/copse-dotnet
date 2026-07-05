using Copse.Core;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<T> SkipTrees<T>(
      this ITreenumerable<T> source,
      int count)
      => source.PruneBefore(step => step.Position.Depth == 0 && step.Position.SiblingIndex < count);

    public static IDepthFirstTreenumerable<T> SkipTrees<T>(
      this IDepthFirstTreenumerable<T> source,
      int count)
      => source.PruneBefore(step => step.Position.Depth == 0 && step.Position.SiblingIndex < count);

    public static IBreadthFirstTreenumerable<T> SkipTrees<T>(
      this IBreadthFirstTreenumerable<T> source,
      int count)
      => source.PruneBefore(step => step.Position.Depth == 0 && step.Position.SiblingIndex < count);
  }
}
