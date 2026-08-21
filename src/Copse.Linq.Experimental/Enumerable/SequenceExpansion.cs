using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Experimental
{
  /// <summary>One item of an expansion's canonical form: a value, or THE SLOT -- the position
  /// where the rest of the stream continues.</summary>
  public readonly struct SlotOrValue<T>
  {
    internal SlotOrValue(T value, bool isSlot)
    {
      Value = value;
      IsSlot = isSlot;
    }

    public T Value { get; }
    public bool IsSlot { get; }
  }

  /// <summary>
  /// One element's expansion under the pointed bind (the sequence LAB for
  /// SELECTMANY_DESIGN.md Addendum II): a lazy stream of items carrying AT MOST ONE SLOT --
  /// the position where the rest of the source stream, already rewritten, continues.
  /// Slotted, the element becomes <c>before ++ [the rest] ++ after</c>; slotless, it
  /// becomes its items alone and the stream STOPS there.
  ///
  /// <para>The canonical form is the tagged stream; the factories are the only way to mint
  /// one, and each emits at most one slot, so "at most one slot" holds by construction.
  /// Two families of factories cover two kinds of placement: A-PRIORI (the slot's position
  /// is known before the expansion produces anything -- <c>Slotted</c>, and its sugar
  /// <c>Return</c>/<c>Promote</c>/<c>Drop</c>/<c>Leaf</c>) and DISCOVERED (the position
  /// depends on the items -- <c>SlotAfter</c>, whose no-match policy also spells
  /// slot-at-end, classic flatten). Discovered placement is AFTER-only: placing a slot
  /// before an item the expansion has already computed would pull that item ahead of its
  /// emission, and this operator's contract is that an expansion is never pulled ahead of
  /// its emission -- pull order, emission order, and effect order coincide. A
  /// content-discovered "before" is a one-item lookahead by nature and is deliberately
  /// absent (revisit hook: a consumer who needs it).</para>
  ///
  /// <para><c>default</c> is the slotless empty -- <c>Drop</c> -- the least-capable value.</para>
  /// </summary>
  public readonly struct SequenceExpansion<T>
  {
    internal SequenceExpansion(IEnumerable<SlotOrValue<T>> items)
    {
      _Items = items;
    }

    private readonly IEnumerable<SlotOrValue<T>> _Items;

    /// <summary>The canonical form: the tagged stream, lazy, at most one slot.</summary>
    public IEnumerable<SlotOrValue<T>> Items => _Items ?? Enumerable.Empty<SlotOrValue<T>>();
  }

  /// <summary>What <see cref="SequenceExpansion.SlotAfter{T}(IEnumerable{T}, Func{T, bool}, IfNoMatch)"/>
  /// does when the predicate never fires: the stream stops, or the slot lands after the
  /// last item (which is classic flatten).</summary>
  public enum IfNoMatch
  {
    Slotless,
    SlotAtEnd,
  }

  /// <summary>The expansion vocabulary's factories (non-generic home, for inference).</summary>
  public static class SequenceExpansion
  {
    // ------------------------------------------------------------- a-priori placement

    /// <summary>[value, slot]: emit the value, continue -- substitution's unit (Select).</summary>
    public static SequenceExpansion<T> Return<T>(T value)
      => new SequenceExpansion<T>(new[] { ValueItem(value), SlotItem<T>() });

    /// <summary>[slot]: emit nothing, continue -- Where's drop arm.</summary>
    public static SequenceExpansion<T> Promote<T>()
      => new SequenceExpansion<T>(new[] { SlotItem<T>() });

    /// <summary>[]: emit nothing, STOP -- TakeWhile's cut; the tree's PruneBefore.</summary>
    public static SequenceExpansion<T> Drop<T>()
      => default;

    /// <summary>[value]: emit the value, STOP -- take-until inclusive; the tree's PruneAfter.</summary>
    public static SequenceExpansion<T> Leaf<T>(T value)
      => new SequenceExpansion<T>(new[] { ValueItem(value) });

    /// <summary>The general a-priori placement: <paramref name="beforeSlot"/>, the
    /// continuation, then <paramref name="afterSlot"/> -- which nests after everything the
    /// rest of the stream expands to. Nothing is enumerated here; slot-first is
    /// <c>Slotted(empty, items)</c>, and the continuation runs before the expansion is
    /// touched at all.</summary>
    public static SequenceExpansion<T> Slotted<T>(IEnumerable<T> beforeSlot, IEnumerable<T> afterSlot)
      => new SequenceExpansion<T>(SlottedItems(beforeSlot, afterSlot));

    /// <summary>The general slotless expansion: emit <paramref name="items"/>, stop.</summary>
    public static SequenceExpansion<T> Slotless<T>(IEnumerable<T> items)
      => new SequenceExpansion<T>(items.Select(ValueItem));

    /// <summary>Slot after the last item: classic flatten.</summary>
    public static SequenceExpansion<T> SlotAtEnd<T>(IEnumerable<T> items)
      => SlotAfter(items, _ => false, IfNoMatch.SlotAtEnd);

    // ----------------------------------------------------------- discovered placement

    /// <summary>The slot goes AFTER the first item satisfying <paramref name="predicate"/>
    /// -- decided on an item already emitted, so nothing is pulled ahead; the predicate is
    /// not consulted again once the slot is placed. When it never fires,
    /// <paramref name="ifNoMatch"/> decides.</summary>
    public static SequenceExpansion<T> SlotAfter<T>(IEnumerable<T> items, Func<T, bool> predicate, IfNoMatch ifNoMatch)
      => SlotAfter(items, (item, _) => predicate(item), ifNoMatch);

    /// <summary>The indexed form: a-priori positions ("after the third item") through the
    /// discovered mechanism, no split of the stream required.</summary>
    public static SequenceExpansion<T> SlotAfter<T>(IEnumerable<T> items, Func<T, int, bool> predicate, IfNoMatch ifNoMatch)
      => new SequenceExpansion<T>(SlotAfterItems(items, predicate, ifNoMatch));

    // ------------------------------------------------------------------- the core

    internal static SlotOrValue<T> ValueItem<T>(T value) => new SlotOrValue<T>(value, isSlot: false);

    internal static SlotOrValue<T> SlotItem<T>() => new SlotOrValue<T>(default, isSlot: true);

    private static IEnumerable<SlotOrValue<T>> SlottedItems<T>(IEnumerable<T> beforeSlot, IEnumerable<T> afterSlot)
    {
      foreach (var item in beforeSlot)
        yield return ValueItem(item);

      yield return SlotItem<T>();

      foreach (var item in afterSlot)
        yield return ValueItem(item);
    }

    private static IEnumerable<SlotOrValue<T>> SlotAfterItems<T>(IEnumerable<T> items, Func<T, int, bool> predicate, IfNoMatch ifNoMatch)
    {
      var placed = false;
      var index = 0;

      foreach (var item in items)
      {
        yield return ValueItem(item);

        if (!placed && predicate(item, index++))
        {
          placed = true;
          yield return SlotItem<T>();
        }
      }

      if (!placed && ifNoMatch == IfNoMatch.SlotAtEnd)
        yield return SlotItem<T>();
    }
  }
}
