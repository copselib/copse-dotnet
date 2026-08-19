using System;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// The async color's algebra over <see cref="Option{TValue}"/>: the same map and bind, for the
  /// two shapes an awaited library hands you -- an option in hand whose next step is awaited, and
  /// a step's pending option you want to chain onto without opening it first. The sync color needs
  /// neither (its steps return the option itself, so the instance members suffice), which is why
  /// this half has no generated twin.
  /// </summary>
  public static class AsyncOption
  {
    /// <summary>Chain an awaited partial step onto an option in hand, short-circuiting the miss.
    /// The miss path allocates no state machine.</summary>
    public static ValueTask<Option<TResult>> BindAsync<TValue, TResult>(
      this Option<TValue> option,
      Func<TValue, ValueTask<Option<TResult>>> selector)
      => option.HasValue ? selector(option.Value) : new ValueTask<Option<TResult>>(default(Option<TResult>));

    /// <summary>The capture-free bind: <paramref name="state"/> travels as an argument, so the
    /// delegate stays a cached static and a step in a loop allocates nothing.</summary>
    public static ValueTask<Option<TResult>> BindAsync<TState, TValue, TResult>(
      this Option<TValue> option,
      TState state,
      Func<TState, TValue, ValueTask<Option<TResult>>> selector)
      => option.HasValue ? selector(state, option.Value) : new ValueTask<Option<TResult>>(default(Option<TResult>));

    /// <summary>Chain onto a step's pending option: <c>await walker.MoveToChildAsync(0)
    /// .BindAsync(child =&gt; child.MoveToChildAsync(1))</c> is the two-step climb, miss included,
    /// without a temporary or a guard.</summary>
    public static async ValueTask<Option<TResult>> BindAsync<TValue, TResult>(
      this ValueTask<Option<TValue>> pending,
      Func<TValue, ValueTask<Option<TResult>>> selector)
    {
      var option = await pending.ConfigureAwait(false);

      return option.HasValue ? await selector(option.Value).ConfigureAwait(false) : default;
    }

    /// <summary>Relabel a step's pending option through an awaited projection, leaving the miss a
    /// miss -- the shape a probe-then-read pair takes.</summary>
    public static async ValueTask<Option<TResult>> MapAsync<TValue, TResult>(
      this ValueTask<Option<TValue>> pending,
      Func<TValue, ValueTask<TResult>> selector)
    {
      var option = await pending.ConfigureAwait(false);

      return option.HasValue ? new Option<TResult>(await selector(option.Value).ConfigureAwait(false)) : default;
    }
  }
}
