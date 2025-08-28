var items = new[] { (1, 2) };

// Same code, but config disables foreach checks, so expect 0 diagnostics.
foreach (var (tmp, val) in items)
{
}
