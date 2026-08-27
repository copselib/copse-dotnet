namespace Copse.Core
{
  /// <summary>
  /// A value that may be absent: <see cref="HasValue"/> says whether <see cref="Value"/> was
  /// produced, and when it is <c>false</c> the value slot is <c>default</c> and must not be read.
  /// This is the library's standard way of answering a question whose miss is an expected
  /// outcome rather than an error -- the parent of a root, the child past the last one. When a
  /// member throws instead of returning an absent option, the call itself was invalid.
  /// </summary>
  public readonly struct Option<TValue>
  {
    /// <summary>Creates a present option carrying <paramref name="value"/>. The absent option is
    /// <c>default</c>.</summary>
    public Option(TValue value)
    {
      HasValue = true;
      Value = value;
    }

    /// <summary>Whether a value is present. When <c>false</c>, <see cref="Value"/> is
    /// <c>default</c> and must not be read.</summary>
    public readonly bool HasValue;

    /// <summary>The value. Valid only when <see cref="HasValue"/> is <c>true</c>; check first.
    /// Exposed as a field rather than a checked property so reading it costs a single load.</summary>
    public readonly TValue Value;

    /// <summary>Tests for a value and assigns it in one expression -- the try-pattern shape, for
    /// contexts like a loop condition that demand a <c>bool</c> and give no way to name the
    /// value they guard. In ordinary statement code, read <see cref="HasValue"/> and
    /// <see cref="Value"/> directly.
    ///
    /// <para>On a miss, <paramref name="value"/> is assigned <c>default</c> (the try-pattern's
    /// contract) -- do not read it after a <c>false</c> return.</para></summary>
    public bool TryGetValue(out TValue value)
    {
      value = Value;

      return HasValue;
    }

    /// <summary>A present option renders as its value; an absent one renders as "none".</summary>
    public override string ToString() => HasValue ? $"{Value}" : "none";
  }
}
