#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace thebasicslanguageunderstanding;

internal sealed class WordPieceTokenizer
{
    private const int DefaultMaxTokens = 128;
    private static readonly Regex BasicTokenRegex = new Regex(@"[\p{L}\p{N}]+|[^\s\p{L}\p{N}]", RegexOptions.Compiled);
    private readonly Dictionary<string, int> _vocab;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _unkId;

    private WordPieceTokenizer(Dictionary<string, int> vocab)
    {
        _vocab = vocab;
        _clsId = GetRequiredTokenId("[CLS]");
        _sepId = GetRequiredTokenId("[SEP]");
        _unkId = GetRequiredTokenId("[UNK]");
    }

    public static WordPieceTokenizer Load(string vocabPath)
    {
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;
        foreach (var line in File.ReadLines(vocabPath))
        {
            var token = line.Trim();
            if (token.Length == 0 || vocab.ContainsKey(token))
            {
                continue;
            }

            vocab[token] = index++;
        }

        return new WordPieceTokenizer(vocab);
    }

    public TokenizedText Tokenize(string text, int maxTokens = DefaultMaxTokens)
    {
        maxTokens = Math.Max(8, maxTokens);
        var tokenIds = new List<long> { _clsId };

        foreach (var token in BasicTokenize(text))
        {
            foreach (var wordPieceId in WordPieceTokenize(token))
            {
                if (tokenIds.Count >= maxTokens - 1)
                {
                    break;
                }

                tokenIds.Add(wordPieceId);
            }

            if (tokenIds.Count >= maxTokens - 1)
            {
                break;
            }
        }

        tokenIds.Add(_sepId);
        var attentionMask = Enumerable.Repeat(1L, tokenIds.Count).ToArray();
        var tokenTypeIds = new long[tokenIds.Count];
        return new TokenizedText(tokenIds.ToArray(), attentionMask, tokenTypeIds);
    }

    private IEnumerable<string> BasicTokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in BasicTokenRegex.Matches(text.ToLowerInvariant()))
        {
            yield return match.Value;
        }
    }

    private IEnumerable<int> WordPieceTokenize(string token)
    {
        if (token.Length > 100)
        {
            yield return _unkId;
            yield break;
        }

        var pieces = new List<int>();
        var start = 0;
        while (start < token.Length)
        {
            var end = token.Length;
            var matched = false;
            while (start < end)
            {
                var piece = token.Substring(start, end - start);
                if (start > 0)
                {
                    piece = "##" + piece;
                }

                if (_vocab.TryGetValue(piece, out var id))
                {
                    pieces.Add(id);
                    start = end;
                    matched = true;
                    break;
                }

                end--;
            }

            if (!matched)
            {
                yield return _unkId;
                yield break;
            }
        }

        foreach (var piece in pieces)
        {
            yield return piece;
        }
    }

    private int GetRequiredTokenId(string token)
    {
        if (_vocab.TryGetValue(token, out var id))
        {
            return id;
        }

        throw new InvalidDataException($"Vocabulary is missing required token {token}.");
    }
}

internal sealed class TokenizedText
{
    public TokenizedText(long[] inputIds, long[] attentionMask, long[] tokenTypeIds)
    {
        InputIds = inputIds;
        AttentionMask = attentionMask;
        TokenTypeIds = tokenTypeIds;
    }

    public long[] InputIds { get; }

    public long[] AttentionMask { get; }

    public long[] TokenTypeIds { get; }

    public int Length => InputIds.Length;
}
