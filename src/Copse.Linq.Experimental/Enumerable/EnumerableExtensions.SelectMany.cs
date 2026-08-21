using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Experimental
{
  public static partial class EnumerableExtensions
  {
    /// <summary>
    /// The pointed bind over sequences (the LAB spelling of SELECTMANY_DESIGN.md Addendum
    /// II): each element is replaced by its expansion, and the rest of the stream -- already
    /// rewritten -- continues at the expansion's slot. A slotless expansion stops the
    /// stream.
    ///
    /// <para>The driver is a stack of PAUSED enumerators -- open brackets. An expansion's
    /// stream is driven until its slot: its items are emitted as they are pulled, then the
    /// enumerator is paused at the slot and pushed. When the source ends, or an expansion
    /// ends without a slot, the brackets close in reverse: each paused enumerator is
    /// resumed and drained. Contract: an expansion is never pulled ahead of its emission --
    /// pull order, emission order, and effect order coincide; the only held state is a
    /// paused enumerator that has not been asked for its next item. Every acquired
    /// enumerator is disposed on full drain, on early termination, and on an exception.
    /// This is the miniature of the tree operator's paused inner treenumerators, one per
    /// path level.</para>
    /// </summary>
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(
      this IEnumerable<TSource> source,
      Func<TSource, SequenceExpansion<TResult>> selector)
    {
      var paused = new Stack<IEnumerator<SlotOrValue<TResult>>>();

      try
      {
        foreach (var element in source)
        {
          var cursor = selector(element).Items.GetEnumerator();
          var pausedAtSlot = false;

          try
          {
            while (cursor.MoveNext())
            {
              if (cursor.Current.IsSlot)
              {
                pausedAtSlot = true;
                break;
              }

              yield return cursor.Current.Value;
            }
          }
          finally
          {
            if (!pausedAtSlot)
              cursor.Dispose();
          }

          if (!pausedAtSlot)
            break;                                   // the stream stops; enclosing brackets still close

          paused.Push(cursor);                       // the bracket opens: paused at the slot
        }

        while (paused.Count > 0)
        {
          var cursor = paused.Pop();

          try
          {
            while (cursor.MoveNext())
            {
              if (cursor.Current.IsSlot)
                throw new InvalidOperationException("An expansion carries at most one slot; a second one is a malformed expansion, not a miss.");

              yield return cursor.Current.Value;
            }
          }
          finally
          {
            cursor.Dispose();
          }
        }
      }
      finally
      {
        while (paused.Count > 0)
          paused.Pop().Dispose();                    // early termination: close every bracket still open
      }
    }

    /// <summary>
    /// Kleisli composition, implemented AS the operator: the second selector runs over the
    /// first's tagged stream, the first's slot riding along as an inert VALUE of the outer
    /// bind (emitted through, never consumed), while the second's slots become the outer
    /// bind's continuation. The composite is pointed by construction -- the first's one slot
    /// passes, the second's are consumed -- and lazy end to end.
    /// </summary>
    public static Func<TSource, SequenceExpansion<TResult>> Compose<TSource, TMiddle, TResult>(
      Func<TSource, SequenceExpansion<TMiddle>> first,
      Func<TMiddle, SequenceExpansion<TResult>> second)
      => value => new SequenceExpansion<TResult>(first(value).Items.SelectMany(item => item.IsSlot
        ? SequenceExpansion.Return(SequenceExpansion.SlotItem<TResult>())
        : Lift(second(item.Value))));

    // The second selector's expansion, one level up: its values become outer values (each
    // carrying a value item), its slot becomes the outer slot.
    private static SequenceExpansion<SlotOrValue<TResult>> Lift<TResult>(SequenceExpansion<TResult> expansion)
      => new SequenceExpansion<SlotOrValue<TResult>>(expansion.Items.Select(item => item.IsSlot
        ? SequenceExpansion.SlotItem<SlotOrValue<TResult>>()
        : SequenceExpansion.ValueItem(SequenceExpansion.ValueItem(item.Value))));
  }
}
