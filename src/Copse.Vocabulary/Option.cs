using System;

namespace Copse
{
  /// <summary>
  /// A value that may be absent: <see cref="HasValue"/> says whether <see cref="Value"/> was
  /// produced, and when it is <c>false</c> the value slot is <c>default</c> and must not be read.
  /// The generic form of the library's typed miss -- returned BY VALUE, storing nothing and using
  /// no <c>out</c> param, the shape that stays legal in both colors (an <c>out</c> param is
  /// illegal in an async method; a stored result bloats the enumerator frame).
  ///
  /// <para>An option is what an operation returns when the miss is an expected answer rather than
  /// a violation: the parent of a root, the child past the last one, the step that had nowhere to
  /// stand. The exception channel stays reserved for malformed questions.</para>
  ///
  /// <para>Deliberately NOT equatable. The library never compares values, so that consumers owe it
  /// no <see cref="IEquatable{T}"/>, no comparer, and no <c>Equals</c>/<c>GetHashCode</c>
  /// override; an option over a value inherits that promise and adds no equality of its own.</para>
  /// </summary>
  public readonly struct Option<TValue>
  {
    /// <summary>The inhabited option carrying <paramref name="value"/>. The absent one is
    /// <c>default</c> (or <see cref="Option.None{TValue}"/>, which reads as a word).</summary>
    public Option(TValue value)
    {
      HasValue = true;
      Value = value;
    }

    /// <summary>Whether a value is present. When <c>false</c>, <see cref="Value"/> is
    /// <c>default</c> and reading it is a bug.</summary>
    public readonly bool HasValue;

    /// <summary>The value, valid only when <see cref="HasValue"/> is <c>true</c>. A public field,
    /// not a checked property: reading it is the hot path, and the check the caller already made
    /// is the one this type asks for.</summary>
    public readonly TValue Value;

    /// <summary>The value if present, otherwise <paramref name="fallback"/>. The total read.</summary>
    public TValue GetValueOrDefault(TValue fallback) => HasValue ? Value : fallback;

    /// <summary>The value if present, otherwise <c>default</c>. Never use this where the value's
    /// <c>default</c> is a legitimate answer -- an ordinal handle's <c>default</c> is the root,
    /// and the miss would masquerade as it.</summary>
    public TValue GetValueOrDefault() => Value;

    /// <summary>The try-pattern face, for callers standing outside an <c>await</c>: <c>true</c>
    /// with the value on a hit, <c>false</c> with <c>default</c> on a miss.</summary>
    public bool TryGetValue(out TValue value)
    {
      value = Value;

      return HasValue;
    }

    /// <summary>Relabel the value in place, leaving the miss a miss. The functor map: a step whose
    /// result is fed to a total transformation, without opening the option to do it.</summary>
    public Option<TResult> Map<TResult>(Func<TValue, TResult> selector)
      => HasValue ? new Option<TResult>(selector(Value)) : default;

    /// <summary>The capture-free map: <paramref name="state"/> is handed to the selector instead of
    /// closed over, so the delegate stays a cached static and a step in a loop allocates nothing.
    /// The shape hot code uses when it wants the algebra.</summary>
    public Option<TResult> Map<TState, TResult>(TState state, Func<TState, TValue, TResult> selector)
      => HasValue ? new Option<TResult>(selector(state, Value)) : default;

    /// <summary>Chain a second partial step onto this one, short-circuiting the miss. The monadic
    /// bind: what a climb, a probe sequence, or any run of steps-that-may-fail composes with.</summary>
    public Option<TResult> Bind<TResult>(Func<TValue, Option<TResult>> selector)
      => HasValue ? selector(Value) : default;

    /// <summary>The capture-free bind -- see <see cref="Map{TState,TResult}(TState, Func{TState, TValue, TResult})"/>
    /// for why the state travels as an argument.</summary>
    public Option<TResult> Bind<TState, TResult>(TState state, Func<TState, TValue, Option<TResult>> selector)
      => HasValue ? selector(state, Value) : default;

    /// <summary>Answer both cases at once, so neither is forgotten and neither reads
    /// <see cref="Value"/> unguarded.</summary>
    public TResult Match<TResult>(Func<TValue, TResult> onValue, Func<TResult> onMiss)
      => HasValue ? onValue(Value) : onMiss();

    /// <summary>This option if it is inhabited, otherwise <paramref name="fallback"/> -- the first
    /// hit among alternatives.</summary>
    public Option<TValue> Or(Option<TValue> fallback) => HasValue ? this : fallback;

    /// <summary>Keep the value only when it satisfies <paramref name="predicate"/>; a rejected
    /// value becomes a miss.</summary>
    public Option<TValue> Where(Func<TValue, bool> predicate)
      => HasValue && predicate(Value) ? this : default;

    /// <summary>An inhabited option is its value; the miss reads as a word, never as a blank.</summary>
    public override string ToString() => HasValue ? $"{Value}" : "none";
  }

  /// <summary>Creation surface for <see cref="Option{TValue}"/>: the two constructors as words,
  /// with <see cref="Some{TValue}"/> inferring its type argument where the constructor cannot.</summary>
  public static class Option
  {
    /// <summary>The inhabited option carrying <paramref name="value"/>.</summary>
    public static Option<TValue> Some<TValue>(TValue value) => new Option<TValue>(value);

    /// <summary>The absent option -- the same value as <c>default</c>, spelled for readability at
    /// a <c>return</c>.</summary>
    public static Option<TValue> None<TValue>() => default;
  }
}
