using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniAnalyzers.Core;

/// <summary>
/// Formats a context snippet for a diagnostic location.
/// </summary>
internal static class ContextSnippetBuilder
{
    // Display guardrails. Tweak later if needed.
    private const int MaxLineDisplayChars = 180;    // cap per rendered line
    private const int HighlightPadding = 60;        // chars shown around [| |] on trim
    private const int MaxSnippetChars = 4000;       // cap for the entire snippet

    /// <summary>
    /// Create a formatted multi-line snippet around <paramref name="location"/>.
    /// </summary>
    /// <param name="location">Primary diagnostic location.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="contextLines">Number of lines included above and below the primary line.</param>
    /// <returns>Preformatted snippet ready for a monospaced text control.</returns>
    public static string Build(Location location, System.Threading.CancellationToken ct = default, int contextLines = 2)
    {
        var tree = location.SourceTree;
        if (tree is null) return string.Empty; // No source to show.

        var text = tree.GetText(ct);
        var lineSpan = location.GetLineSpan();

        int startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        int endLine = Math.Min(text.Lines.Count - 1, lineSpan.EndLinePosition.Line + contextLines);

        int absHlStart = location.SourceSpan.Start;
        int absHlEnd = location.SourceSpan.End;

        var sb = new StringBuilder();
        int lineNumberWidth = (endLine + 1).ToString().Length;

        for (int i = startLine; i <= endLine; i++)
        {
            var line = text.Lines[i];
            string lineText = line.ToString();

            bool intersects = absHlStart <= line.End && absHlEnd >= line.Start;

            // Insert highlight markers when the diagnostic touches this line.
            if (intersects)
            {
                int relStart = Math.Clamp(absHlStart - line.Start, 0, lineText.Length);
                int relEnd = Math.Clamp(absHlEnd - line.Start, 0, lineText.Length);
                if (relEnd < relStart) (relStart, relEnd) = (relEnd, relStart);

                // Insert end first, then start, so indices stay valid.
                lineText = lineText.Insert(relEnd, "|]").Insert(relStart, "[|");
            }

            // Trim very long lines. Keep [| |] visible when present.
            if (lineText.Length > MaxLineDisplayChars)
            {
                if (intersects)
                {
                    int markStart = lineText.IndexOf("[|", StringComparison.Ordinal);
                    int markEnd = markStart >= 0 ? lineText.IndexOf("|]", markStart + 2, StringComparison.Ordinal) : -1;

                    if (markStart >= 0 && markEnd >= 0)
                    {
                        int desiredStart = Math.Max(0, markStart - HighlightPadding);
                        int desiredEnd = Math.Min(lineText.Length, markEnd + 2 + HighlightPadding);

                        int window = desiredEnd - desiredStart;
                        if (window > MaxLineDisplayChars)
                        {
                            int extra = window - MaxLineDisplayChars;
                            int leftCut = extra / 2;
                            int rightCut = extra - leftCut;
                            desiredStart += leftCut;
                            desiredEnd -= rightCut;

                            // Avoid cutting inside tokens.
                            if (desiredStart > markStart && desiredStart < markStart + 2) desiredStart = markStart;
                            if (desiredEnd > markEnd && desiredEnd < markEnd + 2) desiredEnd = markEnd + 2;
                        }

                        desiredStart = Math.Max(0, desiredStart);
                        desiredEnd = Math.Min(lineText.Length, desiredEnd);

                        string segment = lineText.Substring(desiredStart, desiredEnd - desiredStart);
                        if (desiredStart > 0) segment = "…" + segment;
                        if (desiredEnd < lineText.Length) segment += "…";
                        lineText = segment;
                    }
                    else
                    {
                        // Tokens unexpectedly not found. Fall back to head trim.
                        lineText = lineText.Substring(0, MaxLineDisplayChars) + "…";
                    }
                }
                else
                {
                    // No highlight on this line: show the head only.
                    lineText = lineText.Substring(0, MaxLineDisplayChars) + "…";
                }
            }

            string indicator = i == lineSpan.StartLinePosition.Line ? ">" : " ";
            sb.Append((i + 1).ToString().PadLeft(lineNumberWidth))
              .Append(' ')
              .Append(indicator)
              .Append(' ')
              .AppendLine(lineText);

            // Early stop if we built enough content.
            if (sb.Length > MaxSnippetChars)
            {
                sb.AppendLine("… [truncated]");
                break; // explicit so there is no “empty body” confusion
            }
        }

        var result = sb.ToString();
        if (result.Length > MaxSnippetChars)
            result = result.Substring(0, MaxSnippetChars) + Environment.NewLine + "… [truncated]";

        return result;
    }
}


