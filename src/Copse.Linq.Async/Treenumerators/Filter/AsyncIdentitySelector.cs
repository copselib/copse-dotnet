using Copse.Core;
using System;

namespace Copse.Linq.Async
{
  // The cached identity projection: plain Where/Prune instantiate the genericized filter
  // drivers as <TNode, TNode> with this selector, so the unfused operators and the fused
  // Select* variants share one machinery. One allocation per closed TNode for the lifetime
  // of the process -- never per operator, never per node.
  internal static class AsyncIdentitySelector<TNode>
  {
    internal static readonly Func<NodeContext<TNode>, TNode> Instance = nodeContext => nodeContext.Node;
  }
}
