using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure;

internal static class OptionReaders
{
    public static int ReadInt(AnalyzerConfigOptions o, string key, int def, int min, int max)
    {
        if (!o.TryGetValue(key, out var raw) || !int.TryParse(raw, out var n)) return def;
        if (n < min) n = min;
        if (n > max) n = max;
        return n;
    }

    public static bool ReadBool(AnalyzerConfigOptions o, string key, bool def)
    {
        if (!TryGet(o, key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return def;

        raw = raw.Trim().ToLowerInvariant();
        return raw switch
        {
            "true" => true,
            "1" => true,
            "false" => false,
            "0" => false,
            _ => def
        };
    }

    public static IImmutableSet<string> ReadSet(AnalyzerConfigOptions o, string key)
    {
        if (!TryGet(o, key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return ImmutableHashSet<string>.Empty;

        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim().Trim('"', '\''))
                       .Where(s => s.Length > 0);

        return parts.ToImmutableHashSet(StringComparer.Ordinal);
    }

    public static bool TryGet(AnalyzerConfigOptions o, string key, out string raw)
    {
        // Ensure we only assign non-null values to 'raw'.
        if (o.TryGetValue(key, out var candidate) && candidate is not null)
        {
            raw = candidate;
            return true;
        }

        if (o.TryGetValue(key.ToLowerInvariant(), out candidate) && candidate is not null)
        {
            raw = candidate;
            return true;
        }

        raw = string.Empty;
        return false;
    }
}
