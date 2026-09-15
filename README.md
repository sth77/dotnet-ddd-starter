# dotnet-ddd-starter

A seed project for **tactical Domain-Driven Design on .NET 10**, with the conventions baked in and enforced by the
build. It is the .NET counterpart of [`sth77/spring-ddd-starter`](https://github.com/sth77/spring-ddd-starter):
the same intent, realised with the idioms a .NET architect would reach for. The reasoning behind every choice is in
[`docs/dotnet-ddd-starter-design.md`](docs/dotnet-ddd-starter-design.md) (design) and
[`docs/architectural-decisions.md`](docs/architectural-decisions.md) (decisions, alternatives, deviations).

It is a **kernel, not a menu** (ADR-018): the smallest set that enforces the architecture, built with the
platform's own means wherever they suffice. Runtime dependencies beyond Microsoft packages: the Npgsql EF
provider and one naming convention. Everything else that a starter usually drags in (message bus, value-object
generator, assertion DSL, snapshot library, migration tool) is hand-rolled in a few hundred readable lines, with
the upgrade path documented.

What you get:

- **Rings as projects.** Domain, application, infrastructure, HTTP surface and host are separate projects; the
  compiler enforces the direction. The domain references nothing but the BCL.
- **Aggregates with commands and events.** One command record per operation, a `Can(...)` state machine, events
  registered on the aggregate and published through a transactional outbox after commit, consumed idempotently.
- **Strongly-typed identifiers** as `readonly record struct`s (UUID v7), value objects as EF complex types, enums
  as strings, `xmin` optimistic concurrency — all by convention, none by annotation on the model.
- **SQL-first migrations**: hand-written, reviewed scripts applied by a 100-line runner, validated against the EF
  model by an integration test.
- **Explicit HTTP endpoints** with HAL links that appear only when the aggregate's state permits the command *and*
  the caller is authorised for the endpoint. RFC 9457 ProblemDetails for every error. OpenAPI + Scalar UI.
- **Four enforcement layers:** project references, Roslyn analyzers (banned APIs, `DDD0001`, `DDD0002`),
  ArchUnitNET and reflection tests, startup validation (every state-changing endpoint has a policy).
- **A dependency policy that fails the build** for the packages that went commercial in 2025
  ([`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md)).
- **Scaffolding** via `dotnet new` templates and a regeneration check that keeps them honest.

## Quick start

Prerequisites: .NET SDK 10.0.200 or later, a Docker-compatible container runtime.

```bash
docker compose up -d                       # PostgreSQL 17 on localhost:5432 (app/app)
dotnet run --project src/App.Host          # applies the SQL migrations on startup in Development
```

Open <http://localhost:5080/scalar> for the API reference. In Development the API authenticates from headers:

```bash
# create a person, then a sample owned by that person (needs role "user")
curl -s -X POST localhost:5080/api/people -H 'Content-Type: application/json' \
     -H 'X-User: alice' -H 'X-Roles: user' \
     -d '{"name":"Ada Lovelace","email":"ada@example.org"}'

curl -s -X POST localhost:5080/api/samples -H 'Content-Type: application/json' \
     -H 'X-User: alice' -H 'X-Roles: user' \
     -d '{"name":{"en":"Compiler","de":"Compiler"},"description":"First","city":"00000000-0000-7000-8000-000000003000","owner":"<person id>"}'

# as admin the same sample offers a "publish" link; as user it does not
curl -s localhost:5080/api/samples/<sample id> -H 'X-User: root' -H 'X-Roles: user,admin'
```

Reference data (cities) is seeded by migration `V0002`: Lausanne `…1000`, Bern `…3000`, Zurich `…8000` (see
`SeededCities`).

## Solution layout

```
dotnet-ddd-starter.slnx
  src/
    App.Analyzers/        Roslyn analyzers DDD0001 (operations take a command), DDD0002 (no new/default identifiers)
    App.Domain/           domain ring: Common (building blocks), Sample, Person, ReferenceData
    App.Application/      application ring: IEventHandler<T> and the handlers (SampleOwnerNameSynchronizer)
    App.Infrastructure/   EF Core 10 + PostgreSQL, SQL migrations + runner, repositories, outbox/inbox, security
    App.Api/              minimal-API endpoint modules, representations, HAL, ProblemDetails
    App.Host/             Program.cs — the composition root; `--migrate` runs the migrations and exits
  tests/
    App.Domain.Tests/          fast: no host, no database
    App.Infrastructure.Tests/  EF model only: mapping conventions, migration script checks
    App.ArchitectureTests/     ArchUnitNET + reflection rules
    App.IntegrationTests/      Testcontainers (PostgreSQL) + WebApplicationFactory: HTTP contract, outbox, schema validation
  templates/              dotnet new template pack (D4S.Ddd.Templates)
  build/                  banned symbols, licence allowlist, CI and regeneration scripts
  docs/                   design, ADRs, dependency policy
```

## Build gates

Everything below fails the build, in this order (`build/ci.sh` / `build/ci.ps1` run them all):

```bash
dotnet restore                                     # CI=true → --locked-mode (packages.lock.json)
dotnet format --verify-no-changes                  # .editorconfig is the authority
dotnet build -warnaserror                          # nullable + analyzers + banned APIs + DDD000x + licence denylist
dotnet test --filter FullyQualifiedName!~IntegrationTests   # domain, model, architecture
dotnet test tests/App.IntegrationTests             # needs a Docker daemon; includes schema validation
dotnet list package --vulnerable --include-transitive
nuget-license -i dotnet-ddd-starter.slnx -t -a build/allowed-licenses.json
pwsh build/regen-check.ps1                         # templates still produce compiling, tested code
```

### Running the integration tests

They need a reachable Docker daemon (Testcontainers). On Linux/macOS and in CI that is automatic. On a Windows
machine without Docker Desktop but with Docker Engine inside WSL 2, either expose the daemon on
`tcp://127.0.0.1:2375` and set `DOCKER_HOST`, or run the suite from WSL with a user-space SDK:

```bash
wsl -e bash -lc 'cd /mnt/c/path/to/dotnet-ddd-starter && ~/.dotnet/dotnet test tests/App.IntegrationTests -p:ArtifactsPath=$HOME/artifacts/ddd'
```

`ArtifactsPath` keeps the Linux build output out of the Windows `bin/obj` folders. See ADR-014.

## Working with the code

**Adding a feature.** Install the templates once (`dotnet new install ./templates`) and run from the repo root:

```bash
dotnet new ddd-aggregate -n Todo        # domain, persistence, endpoints, tests, CREATE TABLE script
dotnet new ddd-refdata   -n Country     # reference data entity, repository, read-only endpoints, script
```

Then follow the two printed steps (register the repository, check the migration version number). Details in
[`templates/README.md`](templates/README.md) and ADR-017.

**Changing the schema.** Add a script `src/App.Infrastructure/Migrations/V000N__what_changed.sql`; never edit an
applied one (the runner refuses by checksum). `SchemaValidationTests` fails when the database no longer matches the
EF model. Deployments apply migrations with a one-shot `dotnet App.Host.dll --migrate`, not from application
replicas.

**Adding a dependency.** Register it in `docs/DEPENDENCIES.md` first, then add the version to
`Directory.Packages.props`, then reference it — and argue in an ADR why the kernel needs it. Class C licences (RPL,
AGPL, GPL) are prohibited; MediatR ≥ 13, AutoMapper ≥ 15, MassTransit v9 and FluentAssertions ≥ 8 fail the build
(`D4S-0001`).

**Rules of the road** for module boundaries, commands, events, repositories, endpoints and tests are in
[`INSTRUCTIONS.md`](INSTRUCTIONS.md). Coding agents read [`AGENTS.md`](AGENTS.md).

## Known failure signatures

| You see | It means | Do |
|---|---|---|
| `SchemaValidationTests` fails: `samples.foo is missing` | the EF model and the SQL scripts drifted | add a migration script (or fix the mapping) |
| `Migration V000N was modified after it was applied` | an applied script was edited | revert it and add a new script |
| `DDD0001` | a public state-changing aggregate method does not take a command first | add a nested record to the aggregate's command hierarchy |
| `DDD0002` | `new SampleId()` or `default(SampleId)` | `SampleId.New()` or `SampleId.From(...)` |
| `D4S-0001` | a restrictively licensed package was referenced | see `docs/DEPENDENCIES.md` §2 for the replacement |
| `NU1510 … will not be pruned` | the package is part of the .NET 10 shared framework | remove the `PackageReference` |
| `No suitable constructor was found for the type …` (EF) | a value object nested in another needs a parameterless constructor; get-only properties need explicit `Property(...)` | see ADR-006 |
| `Dead-lettered outbox message: …` in a test, or an `outbox_messages.failed_at` row | a handler failed `MaxAttempts` times | fix the handler; clear `failed_at` to redeliver |
| `State-changing endpoints without an authorisation policy: …` at startup | a POST/PUT/PATCH/DELETE under `/api` has no policy | add `.RequireAuthorization(Policies.X)` |
| `Authentication:Mode=DevelopmentHeaders is only allowed in …` | header authentication configured outside Development/Testing | configure `Authentication:Jwt` |
