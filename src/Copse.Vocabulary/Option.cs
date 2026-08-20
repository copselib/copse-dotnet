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

    /// <summary>Test and bind in one expression, for the one place C# offers nothing else: a
    /// loop condition demands a <c>bool</c> and gives no way to name the value it guards. This is
    /// not a second door -- the door's miss is already typed, and this option IS that miss; it is
    /// the adapter between the type and the language's statement grammar. Anywhere a statement
    /// will do, read <see cref="HasValue"/> and <see cref="Value"/> instead.
    ///
    /// <para>Assigns <paramref name="value"/> on the miss too (<c>default</c>, the try-pattern's
    /// own contract), so a loop that reuses its stance as the target ends holding a default. Fine
    /// where the variable dies with the loop; a bug the moment it is read after.</para></summary>
    public bool TryGetValue(out TValue value)
    {
      value = Value;

      return HasValue;
    }

    /// <summary>Relabel the value in place, leaving the miss a miss -- a probe's hit becoming the
    /// thing the caller wanted, without opening the option to do it. <paramref name="state"/> is
    /// handed to the selector rather than closed over, so the lambda captures nothing and its
    /// delegate is cached once instead of allocated per call.
    ///
    /// <para>Not for per-node code: a delegate CALL there costs more than the allocation this
    /// avoids (measured -- the walker's steps went back to a ternary for exactly that reason).
    /// This is for the once-per-acquisition doors, where it reads better than the branch.</para></summary>
    public Option<TResult> Map<TState, TResult>(TState state, Func<TState, TValue, TResult> selector)
      => HasValue ? new Option<TResult>(selector(state, Value)) : default;

    /// <summary>An inhabited option is its value; the miss reads as a word, never as a blank.</summary>
    public override string ToString() => HasValue ? $"{Value}" : "none";
  }
}

