using System;

namespace Copse.Core
{
  [Flags]
  public enum NodeTraversalStrategies
  {
    TraverseAll = 0,                                         // 0
    SkipNode = 1,                                         // 1
    SkipDescendants = 2,                                         // 2
    SkipNodeAndDescendants = SkipNode | SkipDescendants,                // 3
    SkipSiblings = 4,                                         // 4
    SkipNodeAndSiblings = SkipNode | SkipSiblings, // 5
    SkipDescendantsAndSiblings = SkipDescendants | SkipSiblings, // 6
    SkipAll = SkipNode | SkipDescendants | SkipSiblings, // 7
  }
}
