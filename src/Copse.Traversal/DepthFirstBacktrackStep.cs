namespace Copse.Traversal
{
  /// <summary>What the level the engine just backtracked to needs next (from <c>PopFinishedLevelAndClassify</c>).</summary>
  internal enum DepthFirstBacktrackStep
  {
    GoToRoot,          // The whole forest path is unwound; schedule the next root.
    PromoteNextChild,  // No visit owed here; advance this level's enumerator.
    EmitReturnVisit,   // The accepted node here owes its next between/after-children visit.
  }
}
