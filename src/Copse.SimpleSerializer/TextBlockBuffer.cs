using System;

namespace Copse.SimpleSerializer
{
  // The write-side block buffer both payload writers share: characters APPEND synchronously and
  // leave in blocks -- the writers hand [0, Count) to one TextWriter.Write/WriteAsync per drain,
  // never a writer call per character or per token. The dual of the scanners' read-side block
  // buffering, and color-agnostic for the same reason the paths are: appending is pure memory
  // work, so only the drain (which lives in the writers, where the codegen strips the await)
  // differs between the async source and its sync twin.
  //
  // Capacity starts at one drain block and doubles as needed, so a single oversized token never
  // splits; the writers drain whenever Count reaches DrainThreshold, keeping the steady-state
  // footprint at ~one block plus the longest token.
  internal sealed class TextBlockBuffer
  {
    public const int DrainThreshold = 4096;

    private char[] _Block = new char[DrainThreshold];

    public int Count { get; private set; }

    // The backing block for a drain: write [0, Count), then Clear.
    public char[] Chars => _Block;

    public void Append(char character)
    {
      if (Count == _Block.Length)
        Grow(Count + 1);

      _Block[Count++] = character;
    }

    public void Append(string value)
    {
      if (Count + value.Length > _Block.Length)
        Grow(Count + value.Length);

      value.CopyTo(0, _Block, Count, value.Length);
      Count += value.Length;
    }

    public void Clear() => Count = 0;

    private void Grow(int required)
    {
      var capacity = _Block.Length;

      while (capacity < required)
        capacity *= 2;

      var grown = new char[capacity];
      Array.Copy(_Block, grown, Count);
      _Block = grown;
    }
  }
}
