using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniAnalyzers.UI;

/// <summary>Lightweight view model for a diagnostic rule.</summary>
public sealed record RuleSummary(string Id, string Title);
