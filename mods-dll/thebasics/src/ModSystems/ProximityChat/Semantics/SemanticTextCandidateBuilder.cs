#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal static class SemanticTextCandidateBuilder
{
    private static readonly Regex WordRegex = new Regex(@"\w+", RegexOptions.Compiled);

    public static IEnumerable<WordToken> Tokenize(string message)
    {
        return TokenizeWithOriginalWordCount(message, out _);
    }

    public static List<WordToken> TokenizeWithOriginalWordCount(string message, out int originalWordCount)
    {
        var tokens = new List<WordToken>();
        originalWordCount = 0;
        if (string.IsNullOrWhiteSpace(message))
        {
            return tokens;
        }

        foreach (var text in WordRegex.Matches(message).Cast<Match>().Select(match => match.Value))
        {
            var originalWordIndex = originalWordCount++;
            if (!IsUsefulToken(text))
            {
                continue;
            }

            tokens.Add(new WordToken(text, originalWordIndex));
        }

        return tokens;
    }

    public static string NormalizeText(string text)
    {
        return string.Join(" ", Tokenize(text).Select(token => token.Text.ToLowerInvariant()));
    }

    public static IEnumerable<SemanticCandidateSpan> BuildTokenChunks(
        List<WordToken> tokens,
        int maxChunkWords,
        int overlapWords,
        ISet<string>? priorityTokens = null)
    {
        if (tokens.Count == 0)
        {
            yield break;
        }

        var chunks = new List<(int Start, int End, string Text, int Priority)>();
        var chunkWords = Math.Max(1, maxChunkWords);
        var overlap = Math.Max(0, Math.Min(overlapWords, chunkWords - 1));
        var step = Math.Max(1, chunkWords - overlap);
        for (var start = 0; start < tokens.Count; start += step)
        {
            var end = Math.Min(tokens.Count, start + chunkWords);
            var text = NormalizeTokenRange(tokens, start, end);
            if (!string.IsNullOrWhiteSpace(text))
            {
                chunks.Add((start, end, text, CountPriorityTokens(tokens, start, end, priorityTokens)));
            }

            if (end >= tokens.Count)
            {
                break;
            }
        }

        foreach (var chunk in chunks
            .OrderByDescending(chunk => chunk.Priority)
            .ThenBy(chunk => chunk.Start))
        {
            yield return new SemanticCandidateSpan(chunk.Start, chunk.End, chunk.Text);
        }
    }

    public static IEnumerable<SemanticCandidateSpan> BuildCandidateSpans(
        List<WordToken> tokens,
        int maxSpanWords,
        int maxSpans,
        int startTokenIndex = 0,
        int? endTokenIndex = null,
        ISet<string>? priorityTokens = null)
    {
        if (!TryGetCandidateRange(tokens, maxSpans, startTokenIndex, endTokenIndex, out var startIndex, out var endIndex))
        {
            yield break;
        }

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var windowSize in GetCandidateWindowSizes(Math.Min(maxSpanWords, endIndex - startIndex)))
        {
            foreach (var candidate in BuildWindowCandidates(tokens, startIndex, endIndex, windowSize, emitted, priorityTokens)
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Start))
            {
                yield return new SemanticCandidateSpan(candidate.Start, candidate.Start + windowSize, candidate.Text);
                count++;
                if (count >= maxSpans)
                {
                    yield break;
                }
            }
        }
    }

    private static bool TryGetCandidateRange(
        List<WordToken> tokens,
        int maxSpans,
        int startTokenIndex,
        int? endTokenIndex,
        out int startIndex,
        out int endIndex)
    {
        startIndex = 0;
        endIndex = 0;
        if (tokens.Count == 0 || maxSpans <= 0)
        {
            return false;
        }

        startIndex = Math.Max(0, Math.Min(startTokenIndex, tokens.Count));
        endIndex = Math.Max(startIndex, Math.Min(endTokenIndex ?? tokens.Count, tokens.Count));
        return startIndex < endIndex;
    }

    private static IEnumerable<(int Start, string Text, int Priority)> BuildWindowCandidates(
        List<WordToken> tokens,
        int startIndex,
        int endIndex,
        int windowSize,
        ISet<string> emitted,
        ISet<string>? priorityTokens)
    {
        for (var start = startIndex; start <= endIndex - windowSize; start++)
        {
            var normalized = NormalizeTokenRange(tokens, start, start + windowSize);
            if (string.IsNullOrWhiteSpace(normalized) || !emitted.Add(normalized) || normalized.Length > 80)
            {
                continue;
            }

            yield return (start, normalized, CountPriorityTokens(tokens, start, start + windowSize, priorityTokens));
        }
    }

    private static int CountPriorityTokens(List<WordToken> tokens, int startIndex, int endIndex, ISet<string>? priorityTokens)
    {
        if (priorityTokens == null || priorityTokens.Count == 0)
        {
            return 0;
        }

        var score = 0;
        for (var index = startIndex; index < endIndex && index < tokens.Count; index++)
        {
            if (priorityTokens.Contains(tokens[index].Text))
            {
                score++;
            }
        }

        return score;
    }

    private static IEnumerable<int> GetCandidateWindowSizes(int maxSpanWords)
    {
        if (maxSpanWords >= 2)
        {
            yield return 2;
        }

        if (maxSpanWords > 2)
        {
            yield return maxSpanWords;
        }

        for (var windowSize = 3; windowSize < maxSpanWords; windowSize++)
        {
            yield return windowSize;
        }

        yield return 1;
    }

    private static string NormalizeTokenRange(List<WordToken> tokens, int startIndex, int endIndex)
    {
        return string.Join(" ", tokens
            .Skip(startIndex)
            .Take(Math.Max(0, endIndex - startIndex))
            .Select(token => token.Text.ToLowerInvariant()));
    }

    private static bool IsUsefulToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token) && token.Length >= 3 && token.Any(char.IsLetterOrDigit);
    }
}
