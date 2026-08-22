namespace Copse.Dags
{
  public static partial class DagWalker
  {
    /// <summary>Extend of the identity -- the definition: every node relabeled with the walker standing there.</summary>
    public static DagWalker<DagWalker<TValue, THandle, TEdge>, THandle, TEdge> Duplicate<TValue, THandle, TEdge>(
      this DagWalker<TValue, THandle, TEdge> walker)
      => walker.Extend(focus => focus);
  }
}
