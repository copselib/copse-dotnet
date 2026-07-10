using Copse;
using System;

namespace Copse.Linq
{
  // The write-side handle RootfixDispatch hands its survey: one per child of the surveyed node,
  // carrying the child's source context and accepting EXACTLY ONE Dispatch -- the shape a
  // setter-callback allocator plugs into verbatim ((child, amount) => child.Dispatch(amount) is
  // its assignment callback). A second Dispatch throws immediately; a target left unset throws
  // when the survey returns -- the operator-level analogue of a strict allocator's no-value-lost
  // validation. Shared by the sync RootfixDispatch and its async analog.
  public sealed class DispatchTarget<TSource, TDispatch>
  {
    internal DispatchTarget(int index, NodeContext<TSource> context)
    {
      Index = index;
      Context = context;
    }

    // The child's slot in the flat pre-order result the operator is building.
    internal int Index { get; }

    /// <summary>The child's source value and position.</summary>
    public NodeContext<TSource> Context { get; }

    /// <summary>The child's source value (shorthand for <c>Context.Node</c>).</summary>
    public TSource Node => Context.Node;

    private bool _HasDispatched;
    private TDispatch _Dispatched;

    public void Dispatch(TDispatch value)
    {
      if (_HasDispatched)
        throw new InvalidOperationException(
          $"'{Context.Node}' was dispatched to twice; each target accepts exactly one Dispatch per survey.");

      _HasDispatched = true;
      _Dispatched = value;
    }

    internal TDispatch GetDispatchedOrThrow()
    {
      if (!_HasDispatched)
        throw new InvalidOperationException(
          $"The survey completed without dispatching to '{Context.Node}'; every child must receive exactly one Dispatch.");

      return _Dispatched;
    }

    public override string ToString() => Context.ToString();
  }
}
