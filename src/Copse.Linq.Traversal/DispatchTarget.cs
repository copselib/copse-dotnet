using Copse;
using System;

namespace Copse.Linq
{
  // The write-side handle RootfixDispatch hands its survey: one per child of the surveyed node,
  // carrying the child's source context and accepting EXACTLY ONE Dispatch -- the shape a
  // setter-callback allocator plugs into verbatim ((child, amount) => child.Dispatch(amount) is
  // its assignment callback). A second Dispatch throws immediately; a target left unset throws
  // when the survey returns -- the operator-level analogue of a strict allocator's no-value-lost
  // validation.
  //
  // A struct over the build's shared arrival/written arrays: copies share that backing state, so
  // exactly-once holds across copies and a survey allocates nothing per child. Shared by the
  // sync RootfixDispatch and its async analog.
  public readonly struct DispatchTarget<TSource, TDispatch>
  {
    internal DispatchTarget(NodeContext<TSource> context, TDispatch[] arrivals, bool[] written, int index)
    {
      Context = context;
      _Arrivals = arrivals;
      _Written = written;
      _Index = index;
    }

    // The child's slot in the flat pre-order result the operator is building.
    private readonly TDispatch[] _Arrivals;
    private readonly bool[] _Written;
    private readonly int _Index;

    /// <summary>The child's source value and position.</summary>
    public readonly NodeContext<TSource> Context;

    /// <summary>The child's source value (shorthand for <c>Context.Node</c>).</summary>
    public TSource Node => Context.Node;

    public void Dispatch(TDispatch value)
    {
      if (_Written[_Index])
        throw new InvalidOperationException(
          $"'{Context.Node}' was dispatched to twice; each target accepts exactly one Dispatch per survey.");

      _Arrivals[_Index] = value;
      _Written[_Index] = true;
    }

    public override string ToString() => Context.ToString();
  }
}
