namespace Copse.Linq
{
  // The read-side pair RootfixDispatch produces: a source value together with what the dispatch
  // pass delivered to it (the seed for roots; what its parent dispatched to it otherwise). The
  // operator DECORATES rather than replaces -- the result tree has the source's shape with this
  // pair at every node -- so the flavors are compositions, not overloads: project the pair away
  // with Select for immutable values, or apply it with Do (then unwrap) for mutable ones.
  // Shared by the sync RootfixDispatch and its async analog.
  public readonly struct DispatchNode<TSource, TDispatch>
  {
    public DispatchNode(TSource value, TDispatch dispatched)
    {
      Value = value;
      Dispatched = dispatched;
    }

    public readonly TSource Value;
    public readonly TDispatch Dispatched;

    public override string ToString() => $"{Value} <- {Dispatched}";
  }
}
