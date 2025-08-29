using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure.Common;

internal static class OptionReaders
{
    public static int ReadInt(AnalyzerConfigOptions options, string optionKey, int defaultValue, int min, int max)
    {
        if (!TryGet(options, optionKey, out var raw) || !int.TryParse(raw, out var parsedValue))
            return defaultValue;

        if (parsedValue < min) parsedValue = min;
        if (parsedValue > max) parsedValue = max;
        return parsedValue;
    }

    public static bool ReadBool(AnalyzerConfigOptions options, string optionKey, bool defaultValue)
    {
        if (!TryGet(options, optionKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        raw = raw.Trim().ToLowerInvariant();
        return raw switch
        {
            "true" => true,
            "1" => true,
            "false" => false,
            "0" => false,
            _ => defaultValue
        };
    }

    public static IImmutableSet<string> ReadSet(AnalyzerConfigOptions options, string optionKey)
    {
        if (!TryGet(options, optionKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return ImmutableHashSet<string>.Empty;

        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(segment => segment.Trim().Trim('"', '\''))
                       .Where(segment => segment.Length > 0);

        return parts.ToImmutableHashSet(StringComparer.Ordinal);
    }

    public static bool TryGet(AnalyzerConfigOptions options, string optionKey, out string raw)
    {
        // Ensure we only assign non-null values to 'raw'.
        if (options.TryGetValue(optionKey, out var candidate) && candidate is not null)
        {
            raw = candidate;
            return true;
        }

        if (options.TryGetValue(optionKey.ToLowerInvariant(), out candidate) && candidate is not null)
        {
            raw = candidate;
            return true;
        }

        raw = string.Empty;
        return false;
    }
}
