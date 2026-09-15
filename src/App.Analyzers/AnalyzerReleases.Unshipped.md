; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DDD0001 | Design | Error | Aggregate operations take a command as their first parameter
DDD0002 | Design | Error | Identifiers are created through their New()/From() factories, never with new or default
