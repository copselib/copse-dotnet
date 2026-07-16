using Copse.Core;

namespace Copse.Linq.Async.Treenumerables
{
  // The verdict seam as a struct-generic contract (the engines' TChildEnumerator idiom): the
  // filter drivers are generic over TVerdictSelector : struct, IVerdictSelector<,>, so the JIT
  // monomorphizes per selector type and inlines GetVerdict -- a plain Where's per-node cost
  // compiles back to exactly one indirect call (the user's predicate), where a delegate-typed
  // seam would pay an un-inlinable second call forever. Implementations MUST be stateless
  // readonly structs: the drivers hold them in readonly fields, where a mutating call would
  // silently operate on a defensive copy.
  internal interface IVerdictSelector<TInner, TNode>
  {
    FusionVerdict<TNode> GetVerdict(NodeContext<TInner> nodeContext);
  }
}
