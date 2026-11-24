; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TEAM004 | Maintainability | Error | JustifiedSuppressionAnalyzer: suppressions must name an issue
TEAM001 | Design | Error | MongoOnlyInRepositoriesAnalyzer: MongoDB.Driver types only in *.Repositories
TEAM002 | Design | Error | NlogOnlyInProgramAnalyzer: NLog API only in Program.cs
TEAM003 | Security | Error | NoSecretsInLogsAnalyzer: no secrets or personal data in log calls
TEAM005 | Maintainability | Error | ReadabilityLimitsAnalyzer: no dynamic, goto, query syntax, nested ternaries or deeply nested lambdas
TEAM006 | Reliability | Error | TestHasAssertionAnalyzer: every NUnit test has an assertion or a Moq Verify
TEAM007 | Reliability | Error | NoWaitInTestsAnalyzer: no Thread.Sleep or Task.Delay in test projects
TEAM008 | Naming | Error | ModelTypesInGlossaryAnalyzer: public types in *.Models are in GLOSSARY.md
TEAM009 | Design | Error | ControllerActionContractAnalyzer: controller actions have an HTTP verb and a typed result
TEAM010 | Design | Error | NoHardCodedUrlsAnalyzer: no hard-coded http, https, mongodb or amqp addresses in src/
TEAM012 | Style | Error | ConditionalExpressionAnalyzer: no conditional expression in place of if/return
