; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TEAM004 | Maintainability | Error | JustifiedSuppressionAnalyzer: suppressions must name an issue
TEAM007 | Reliability | Error | NoWaitInTestsAnalyzer: no Thread.Sleep or Task.Delay in test projects
