
namespace Copse.Linq.Treenumerables
{
  // The result seam as a struct-generic contract (the engines' TChildEnumerator idiom): the
  // filter drivers are generic over TResultSelector : struct, IAsyncResultSelector<,>, so the JIT
  // monomorphizes per selector type and inlines GetResult -- a plain Where's per-node cost
  // compiles back to exactly one indirect call (the user's predicate), where a delegate-typed
  // seam would pay an un-inlinable second call forever. Implementations MUST be stateless
  // readonly structs: the drivers hold them in readonly fields, where a mutating call would
  // silently operate on a defensive copy.
  internal interface IAsyncResultSelector<TSource, TResult>
  {
    AsyncSelectWhereResult<TResult> GetResult(NodeAndPosition<TSource> nodeAndPosition);
  }
}
