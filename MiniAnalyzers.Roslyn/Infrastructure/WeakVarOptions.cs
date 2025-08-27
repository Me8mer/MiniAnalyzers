// Roslyn/Infrastructure/WeakVarOptions.cs
using System.Collections.Immutable;

namespace MiniAnalyzers.Roslyn.Infrastructure
{
    /// <summary>Typed options for MNA0004 (weak variable names).</summary>
    internal sealed record WeakVarOptions(
        int MinLength = 3,
        IImmutableSet<string>? AllowedNames = null,
        IImmutableSet<string>? WeakNames = null,
        bool CheckForeach = true)
    {
        public IImmutableSet<string> Allowed => AllowedNames ?? ImmutableHashSet<string>.Empty;
        public IImmutableSet<string> Weak => WeakNames ?? ImmutableHashSet<string>.Empty;
    }
}
