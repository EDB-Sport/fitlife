; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TEAM004 | Maintainability | Error | JustifiedSuppressionAnalyzer: suppressions must name an issue
TEAM005 | Maintainability | Error | ReadabilityLimitsAnalyzer: no dynamic, goto, query syntax, nested ternaries or deeply nested lambdas
