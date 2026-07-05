using System;

namespace Copse.SimpleSerializer
{
  // PreorderStringStore's dual for bft-layout payloads ("a;b,c;d,e"): a lazily-parsed
  // ILevelOrderStore. Groups arrive positionally (group 0 is the roots; group k+1 holds the
  // children of node k, terminated by '|' or ';'; end of string means every remaining group is
  // empty), and each parse step commits one value or closes one group -- exactly as far as some
  // traversal's frontier demands.
  //
  // The string is its own random-access character buffer, so the store affords BOTH dimensions
  // (full ITreenumerable citizenship via LevelOrderTreenumerable: breadth-first is native
  // playback, depth-first rides the child spans cross-order). One store is shared by every
  // treenumerator of the same Deserialize result: parse once, replay many. Single-threaded by
  // contract.
  internal sealed class LevelOrderStringStore<TValue> : ILevelOrderStore<TValue>
  {
    public LevelOrderStringStore(string tree, SpanMap<TValue> map)
    {
      _Tree = tree;
      _Map = map;
    }

    private readonly string _Tree;
    private readonly SpanMap<TValue> _Map;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _FirstChildIndices = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _ChildCounts = new RefAppendOnlyList<int>();

    private int _RootCount;
    private int _CurrentGroupOwner = -1; // whose group the cursor is inside; -1 = the roots group
    private int _Cursor;
    private int _ValueStart = -1;
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

    public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];

    public TValue GetValue(int index) => _Values[index];

    // Advance the parse until it makes progress an Ensure loop can observe: a value committed,
    // a group closed, or the string exhausted (all remaining groups are empty).
    private void ParseStep()
    {
      while (_Cursor < _Tree.Length)
      {
        switch (_Tree[_Cursor])
        {
          case ',':
          {
            var committed = TryCommit();
            _Cursor++;

            if (committed)
              return;

            break;
          }

          case '|':
          case ';':
          {
            TryCommit();
            _Cursor++;
            _CurrentGroupOwner++;

            return;
          }

          case '\n':
          case '\r':
            _Cursor++;
            break;

          case '(':
          case ')':
            throw new FormatException(
              $"Unexpected '{_Tree[_Cursor]}' at index {_Cursor}: this is a depth-first structural " +
              "character, so the string is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

          default:
            if (_ValueStart < 0)
              _ValueStart = _Cursor;

            _Cursor++;
            break;
        }
      }

      TryCommit();
      _Exhausted = true;
    }

    private bool TryCommit()
    {
      if (_ValueStart < 0)
        return false;

      var index = _Values.Count;

      _Values.AddLast(_Map(_Tree.AsSpan(_ValueStart, _Cursor - _ValueStart)));
      _FirstChildIndices.AddLast(-1);
      _ChildCounts.AddLast(0);
      _ValueStart = -1;

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

      return true;
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

      public bool EnsureRootAvailable(int k) => _Store.EnsureRootAvailable(k);
      public bool EnsureChildAvailable(int parentIndex, int k) => _Store.EnsureChildAvailable(parentIndex, k);
      public int GetFirstChildIndex(int parentIndex) => _Store.GetFirstChildIndex(parentIndex);
      public TValue GetValue(int index) => _Store.GetValue(index);
    }
  }
}
