using Copse.Stores;
using System;
using System.Runtime.CompilerServices;

namespace Copse.SimpleSerializer
{
  // PreorderStringStore's dual for bft-layout payloads ("a;b,c;d,e"): a lazily-parsed
  // ILevelOrderStore. Groups arrive positionally (group 0 is the roots; group k+1 holds the
  // children of node k, terminated by '|' or ';'; end of string means every remaining group is
  // empty), and each parse step commits one value or closes one group -- exactly as far as some
  // traversal's frontier demands.
  //
  // Values ride the shared value-token layer (ValueTokenStringScanner): quoted values may
  // contain ANY character, unquoted trailing line endings at end of input are ignored (files
  // end in newlines), and the zero-copy span mapping survives for every token that is a
  // contiguous slice of the source. An UNQUOTED depth-first structural character ('(' or ')')
  // proves the string is the wrong layout.
  //
  // The string is its own random-access character buffer, so the store affords BOTH dimensions
  // (full ITreenumerable citizenship via LevelOrderTreenumerable: breadth-first is native
  // playback, depth-first rides the child spans cross-order). One store is shared by every
  // treenumerator of the same Deserialize result: parse once, replay many. Single-threaded by
  // contract.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): level-order x growing x text-parse feed.
  internal sealed class LevelOrderStringStore<TValue> : ILevelOrderStore<TValue>
  {
    public LevelOrderStringStore(string tree, SpanMap<TValue> map)
    {
      _Scanner = new ValueTokenStringScanner(tree);
      _Map = map;
    }

    private readonly ValueTokenStringScanner _Scanner;
    private readonly SpanMap<TValue> _Map;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _FirstChildIndices = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _ChildCounts = new RefAppendOnlyList<int>();

    private int _RootCount;
    private int _CurrentGroupOwner = -1; // whose group the cursor is inside; -1 = the roots group
    private bool _Exhausted;

    public bool EnsureRootAvailable(int k)
    {
      while (!_Exhausted && _CurrentGroupOwner == -1 && _RootCount <= k)
        ParseStep();

      return k < _RootCount;
    }

    public bool EnsureChildAvailable(int parentIndex, int k)
    {
      while (!_Exhausted && _CurrentGroupOwner <= parentIndex && _ChildCounts[parentIndex] <= k)
        ParseStep();

      return k < _ChildCounts[parentIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Values[index];

    // Advance the parse until it makes progress an Ensure loop can observe: a value committed,
    // a group closed, or the string exhausted (all remaining groups are empty).
    private void ParseStep()
    {
      while (_Scanner.TryScanEvent(out var hasValue, out var terminator))
      {
        switch (terminator)
        {
          case ',':
            if (hasValue)
            {
              Commit();
              return;
            }

            break;

          case '|':
          case ';':
            if (hasValue)
              Commit();

            _CurrentGroupOwner++;
            return;

          case '(':
          case ')':
            throw new FormatException(
              $"Unexpected '{terminator}' near index {_Scanner.Position}: this is a depth-first structural " +
              "character, so the string is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

          default: // end of text: every remaining group is empty
            if (hasValue)
              Commit();

            _Exhausted = true;
            return;
        }
      }

      _Exhausted = true;
    }

    private void Commit()
    {
      var index = _Values.Count;

      _Values.AddLast(_Map(_Scanner.ValueChars));
      _FirstChildIndices.AddLast(-1);
      _ChildCounts.AddLast(0);

      if (_CurrentGroupOwner == -1)
      {
        _RootCount++;
      }
      else
      {
        if (_ChildCounts[_CurrentGroupOwner] == 0)
          _FirstChildIndices[_CurrentGroupOwner] = index;

        _ChildCounts[_CurrentGroupOwner]++;
      }
    }

    // The unboxed handle the playback treenumerators are instantiated over: a struct type
    // argument specializes the generic, so the store calls inline instead of interface-
    // dispatching (the same pattern as the memo's store adapters).
    internal readonly struct Handle : ILevelOrderStore<TValue>
    {
      public Handle(LevelOrderStringStore<TValue> store)
      {
        _Store = store;
      }

      private readonly LevelOrderStringStore<TValue> _Store;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool EnsureRootAvailable(int k) => _Store.EnsureRootAvailable(k);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool EnsureChildAvailable(int parentIndex, int k) => _Store.EnsureChildAvailable(parentIndex, k);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public int GetFirstChildIndex(int parentIndex) => _Store.GetFirstChildIndex(parentIndex);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public TValue GetValue(int index) => _Store.GetValue(index);
    }
  }
}
