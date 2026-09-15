# Claude Code

Follow `AGENTS.md` (agent guidance, known failure signatures, non-negotiables) and `INSTRUCTIONS.md` (coding rules).
Decisions and their rationale: `docs/architectural-decisions.md`. Dependency policy: `docs/DEPENDENCIES.md`.

Before reporting a task done: `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, and
`dotnet test --filter FullyQualifiedName!~IntegrationTests`. Run the integration tests when persistence, endpoints
or messaging changed (on this machine from WSL, see README §Running the integration tests).
