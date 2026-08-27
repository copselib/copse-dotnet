using Copse;
using System;

namespace Copse.Linq
{
  // A struct over the build's shared arrival/written arrays: copies share that backing state,
  // so exactly-once holds across copies and a survey allocates nothing per child.
  /// <summary>
  /// One child of a surveyed node, write-side: its source context, and a
  /// <see cref="Dispatch"/> member that must be called exactly once per survey -- a second
  /// call throws immediately, and a target left unset throws when the survey returns. The
  /// shape plugs directly into setter-style allocators:
  /// <c>(child, amount) =&gt; child.Dispatch(amount)</c>.
  /// </summary>
  public readonly struct DispatchTarget<TNode, TDispatch>
  {
    internal DispatchTarget(NodeContext<TNode> context, TDispatch[] arrivals, bool[] written, int index)
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

    /// <summary>The child's node and position.</summary>
    public readonly NodeContext<TNode> Context;

    /// <summary>The child's node (shorthand for <c>Context.Node</c>).</summary>
    public TNode Node => Context.Node;

    /// <summary>Delivers <paramref name="value"/> to this child. Must be called exactly once
    /// per survey: a second call throws <see cref="InvalidOperationException"/> immediately,
    /// and never calling it throws when the survey returns.</summary>
    public void Dispatch(TDispatch value)
    {
      if (_Written[_Index])
        throw new InvalidOperationException(
          $"'{Context.Node}' was dispatched to twice; each target accepts exactly one Dispatch per survey.");

      _Arrivals[_Index] = value;
      _Written[_Index] = true;
    }

    /// <inheritdoc/>
    public override string ToString() => Context.ToString();
  }
}
