; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TEAM004 | Maintainability | Error | JustifiedSuppressionAnalyzer: suppressions must name an issue
TEAM006 | Reliability | Error | TestHasAssertionAnalyzer: every NUnit test has an assertion or a Moq Verify
