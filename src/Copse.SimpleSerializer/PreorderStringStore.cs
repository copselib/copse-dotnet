using System;


namespace Copse.SimpleSerializer
{
  // A depth-first-serialized tree ("a(b(d,e),c)") as a LAZILY-PARSED preorder store: the
  // incremental version of the old eager parse (a value followed by '(' is a parent, backfilled
  // at its matching ')'; ',' separates siblings), pulled one committed value or one subtree
  // close at a time, exactly as far as some traversal's frontier demands. Retiring the eager
  // parse was the point of the whole serialization redesign: composing costs nothing, early-out
  // never touches the rest of the string, and the value map runs once per node ever reached.
  //
  // The string is its own random-access character buffer, so the store affords BOTH dimensions
  // (full ITreenumerable citizenship via PreorderTreenumerable); parsed values and spans are
  // retained as they materialize -- the same growing-capture shape as the memo's DFT buffer,
  // with subtreeSizes[i] == 0 meaning node i's subtree is still OPEN. One store is shared by
  // every treenumerator of the same Deserialize result: parse once, replay many. Single-threaded
  // by contract, like every treenumerator in the library.
  //
  // Detection replaces the retired layout header (see TRAVERSAL_DIMENSION_SPLIT.md): the caller
  // states the layout by choosing DeserializeDepthFirstTree, and a level-order structural
  // character ('|' or ';') proves the string is the wrong layout (or corrupt) -- a FormatException
  // rather than a silently mis-parsed tree.
  internal sealed class PreorderStringStore<TValue> : IPreorderStore<TValue>
  {
    public PreorderStringStore(string tree, SpanMap<TValue> map)
    {
      _Tree = tree;
      _Map = map;
    }

    private readonly string _Tree;
    private readonly SpanMap<TValue> _Map;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _SubtreeSizes = new RefAppendOnlyList<int>();
    private readonly RefSemiDeque<int> _Open = new RefSemiDeque<int>(); // indices of parents whose ')' hasn't been parsed yet

    private int _Cursor;
    private int _ValueStart = -1; // start of the pending value run; -1 = none
    private bool _Exhausted;

    public bool EnsureBuffered(int index)
    {
      while (!_Exhausted && _Values.Count <= index)
        ParseStep();

      return index < _Values.Count;
    }

    public int EnsureSubtreeClosed(int index)
    {
      while (!_Exhausted && _SubtreeSizes[index] == 0)
        ParseStep();

      return _SubtreeSizes[index];
    }

    public int GetSubtreeSize(int index) => _SubtreeSizes[index];

    public TValue GetValue(int index) => _Values[index];

    // Advance the parse until it makes progress an Ensure loop can observe: a value committed,
    // a subtree closed, or the string exhausted (which closes everything still open).
    private void ParseStep()
    {
      while (_Cursor < _Tree.Length)
      {
        switch (_Tree[_Cursor])
        {
          case '(':
          {
            var committed = TryCommit(asParent: true);
            _Cursor++;

            if (committed)
              return;

            break;
          }

          case ',':
          {
            var committed = TryCommit(asParent: false);
            _Cursor++;

            if (committed)
              return;

            break;
          }

          case ')':
          {
            TryCommit(asParent: false);
            _Cursor++;

            var closed = _Open.RemoveLast();
            _SubtreeSizes[closed] = _Values.Count - closed;

            return;
          }

          case '|':
          case ';':
            throw new FormatException(
              $"Unexpected '{_Tree[_Cursor]}' at index {_Cursor}: this is a level-order structural " +
              "character, so the string is not a depth-first-serialized tree (use DeserializeBreadthFirstTree).");

          default:
            if (_ValueStart < 0)
              _ValueStart = _Cursor;

            _Cursor++;
            break;
        }
      }

      TryCommit(asParent: false); // trailing top-level value, if any

      while (_Open.Count > 0)
      {
        var closed = _Open.RemoveLast();
        _SubtreeSizes[closed] = _Values.Count - closed;
      }

      _Exhausted = true;
    }

    private bool TryCommit(bool asParent)
    {
      if (_ValueStart < 0)
        return false;

      var index = _Values.Count;

      _Values.AddLast(_Map(_Tree.AsSpan(_ValueStart, _Cursor - _ValueStart)));
      _SubtreeSizes.AddLast(asParent ? 0 : 1); // a parent's size is backfilled when it closes
      _ValueStart = -1;

      if (asParent)
        _Open.AddLast(index);

      return true;
    }

    // The unboxed handle the playback treenumerators are instantiated over: a struct type
    // argument specializes the generic, so the store calls inline instead of interface-
    // dispatching (the same pattern as the memo's store adapters).
    internal readonly struct Handle : IPreorderStore<TValue>
    {
      public Handle(PreorderStringStore<TValue> store)
      {
        _Store = store;
      }

      private readonly PreorderStringStore<TValue> _Store;

      public bool EnsureBuffered(int index) => _Store.EnsureBuffered(index);
      public int EnsureSubtreeClosed(int index) => _Store.EnsureSubtreeClosed(index);
      public int GetSubtreeSize(int index) => _Store.GetSubtreeSize(index);
      public TValue GetValue(int index) => _Store.GetValue(index);
    }
  }
}
