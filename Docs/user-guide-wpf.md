# MiniAnalyzers WPF — User Guide

> How to start the app is already covered in the project README. This guide explains what you can do in the WPF app itself: browsing a solution or project, running analysis, inspecting results, and filtering.

## What the app does
The app runs a set of Roslyn analyzers over a chosen `.sln` or `.csproj`, then shows each found problem as a row in a results grid. Each row carries the rule id, severity, message, file and position, the analyzer that reported it, the project name, and an optional code snippet that highlights the exact span.

By default the app runs these rules:
* `MNA0001` Avoid `async void`.
* `MNA0002` Do not use empty `catch` blocks.
* `MNA0003` Avoid `Console.Write` and `Console.WriteLine`.
* `MNA0004` Use descriptive names.

The app respects your `.editorconfig` for rule severities and per-rule options, since Roslyn passes those to analyzers during the run. The documentation index describes the supported options for each rule.

---

## Browse and select a target
* Click **Browse** and pick a `.sln` or `.csproj`. The selected path appears in the input field. The file dialog filters for solutions and C# projects.

## Run the analysis
* Click **Analyze**. The app disables interactive controls during the run, shows a running status, and clears previous results.
* The app automatically decides whether to analyze a solution or a single project based on the file extension.
* The status bar shows the number of diagnostics, including how many are currently visible after filtering.

### Cancel a running analysis
* Click **Cancel** to request cancellation. The status changes accordingly and the UI re-enables after the operation stops.

---

## Inspect results
Each diagnostic appears as a row with these fields:
* `Id` such as `MNA0002`
* `Severity` as a text value
* `Message` with the problem description
* `FilePath` with the absolute path
* `Line` and `Column` as one-based numbers
* `Analyzer` with a short title
* `ProjectName`
* `ContextSnippet` with marked span, if available

### Show code context
* Double-click a row to toggle its details view. The details show the code snippet around the problem so you can see the exact context that was analyzed.

---

## Filter your view
Open **Settings** to refine what is shown in the grid. The dialog edits a shared `FilterSettings` instance and the list refreshes immediately when you confirm the dialog. The status text updates to show both total and visible counts.

The app applies filtering with a single predicate over the in-memory results. These checks happen in order: severity gate, id include or exclude, and a free-text search.

### Severity
* Toggle which severities to include. The filter compares the `Severity` string of each row in a case-insensitive way. Unknown values are allowed to avoid hiding data by accident.

### Include or exclude specific rule ids
* Use a comma-separated list of ids to **include**. When the include list is empty, all ids are allowed.  
* Use a comma-separated list of ids to **exclude**. Excluded ids are always filtered out.  
Both lists are parsed into case-insensitive sets for matching.

### Search
* Enter text in the search box to match across several fields. The filter checks the message, analyzer title, file path, project name, and id using case-insensitive contains.

### Known rules list
* The Settings dialog also receives a list of known rules with ids and titles. This helps you copy or pick ids for include or exclude. The list is built from the analyzers’ `SupportedDiagnostics`.

---

## Notes and tips
* The app does not modify files. It only reports findings with helpful context so you can review and fix them in your editor. The per-rule documentation explains what to change and why.
* Many diagnostics carry actionable suggestions inside their metadata which the UI can surface alongside the message. For example, the empty catch and console rules attach a “Suggestion” text in the diagnostic properties used when reporting.
* If you rely on `.editorconfig`, remember that those settings influence what analyzers report and with what severity. The UI then reflects exactly what was produced. See the options summary in the docs index.

---

## Troubleshooting
* If you select an invalid path, the app prompts you to pick a valid solution or project.
* If the analysis fails with an exception, the app shows an error dialog and restores the controls so you can try again.

---

## Related docs
* Documentation index with rule summaries and `.editorconfig` options  
  `Docs/index.md` in the repo.
