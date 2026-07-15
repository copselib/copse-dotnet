namespace Copse.Linq.Experimental.TreeTokenizer
{
  public interface ITreeToken<TNode, TTokenType>
  {
    TTokenType Type { get; }
    TNode Node { get; }
  }
}
