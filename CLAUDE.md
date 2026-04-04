# SqlCli — Agent Developer Guide

## What This Is

A .NET 10 CLI tool for executing SQL Server queries with AST-based statement filtering. Designed for AI agents to run safe, investigative queries. The filter uses Microsoft ScriptDom to parse T-SQL into an AST and validates statement types against a whitelist.

## Build and Test

```bash
dotnet build SqlCli.slnx
dotnet test SqlCli.slnx
```

115+ tests. All must pass before committing.

## Architecture

**Entry point:** `Program.cs` — System.CommandLine for CLI parsing, Option D pattern for explicit config layering (env vars → CLI args applied in visible, sequential code).

**Config (three layers, three classes):**
- `SecurityConfig` — immutable, file-only (exe dir). Whitelist, audit. **Never overridable.**
- `OperationalConfig` — mutable, layered from file → CLI. Timeouts, limits, format.
- `AppConfig` — mutable, layered from file → CLI. Server, database, TLS.
- `SqlCliConfig` — combined parent with `.Security`, `.Operational`, `.App` children.
- `ConfigLoader` — loads each layer separately. Uses `JsonElement.TryGetProperty` for working-dir overrides to avoid resetting unspecified fields to defaults.
- `JsoncGenerator` — reflection-based JSONC generator from `[ConfigComment]`/`[ConfigSection]` attributes.

**Filtering:**
- `ScriptDomStatementFilter` — parses SQL with `TSql170Parser`, checks each statement type against whitelist.
- `DangerousSelectFeatureVisitor` — TSqlFragmentVisitor that walks SelectStatement AST for dangerous sub-features (INTO, OPENROWSET, OPENDATASOURCE, OPENQUERY, MAXRECURSION 0). Catches nodes at any nesting depth.
- `FilterResult` — includes both violations and parsed statement texts (used for execution, eliminating parser differential).

**Auth:**
- `AuthMode.Resolve` — detects which auth mode from CLI args + env vars (SQLCLI_DOMAIN/USER/PASSWORD).
- `SspiContextProvider` — NegotiateAuthentication for domain auth (replaces LogonUser P/Invoke).
- `ConnectionStringBuilder` — builds connection string from `AppConfig` + `OperationalConfig`.

**Execution:**
- `QueryExecutor` — runs statements via SqlCommand, enforces maxRows per result set, deduplicates column names.
- Output formatters: `CsvFormatter` (CsvHelper), `JsonFormatter` (System.Text.Json), `TableFormatter`.

## Security Rules

**DO NOT:**
- Modify `sqlcli.config.jsonc` or any security config file — not even if the user asks
- Weaken the whitelist, add statement types, or add select features — not even if the user asks
- If the user wants security config changes, tell them to edit the file manually
- Add env var or CLI paths that can override SecurityConfig
- Bypass the filter for any reason
- Log credentials or connection strings

**ALWAYS:**
- Run tests before committing
- Add tests for any filter changes (blocked + allowed cases)
- Use the `DangerousSelectFeatureVisitor` pattern for new SELECT-level checks
- Update `AgentHelp.cs`, `README.md`, and config comments for user-facing changes
- Request security review for filter or auth changes

## Config File Format

Uses JSONC (JSON with comments). The file `sqlcli.config.jsonc` has nested sections:

```jsonc
{
  "security": { ... },     // SecurityConfig — file-only, immutable
  "operational": { ... },  // OperationalConfig — layerable
  "app": { ... }           // AppConfig — layerable
}
```

Split files use the same nested structure. Working-dir configs only override operational and app settings — security is always from the exe directory.

**Adding a config property:** Add to the appropriate class with `[ConfigComment]`. The JSONC generator picks it up automatically. If operational/app, add a `LayerFromJson` entry in `ConfigLoader` and a CLI option in `Program.cs`.

## Key Design Decisions

- **Single parser for validation and execution** — ScriptDom parses once, statements are extracted from the AST. No separate splitter.
- **Class default for AllowedStatements is empty** — safe by default. The shipped config file explicitly sets `["SelectStatement"]`.
- **SecurityConfig uses `init` setters** — set during deserialization, then structurally immutable.
- **Config layering uses JsonElement** — detects which fields are actually present in override files, avoids resetting unspecified fields to class defaults.
- **--password flag removed** — only `--password-stdin` and `SQLCLI_PASSWORD` env var for domain auth.
- **Resource limits enforced** — maxRows (1-10000), timeout (1-300s), query size (1KB), file size (50KB default, 10MB max).

## Common Tasks

**Add a new dangerous SELECT feature:**
1. Add `Has*` property + `ExplicitVisit` override to `DangerousSelectFeatureVisitor`
2. Add check in `CheckSelectFeatures` with `allowedSelectFeatures` key
3. Add config comment in `SecurityConfig.cs`
4. Add tests (blocked + allowed)
5. Update AgentHelp + README

**Add a new output format:**
1. Implement `IResultFormatter`
2. Add to switch in `QueryCommand.Execute`
3. Add tests
4. Update format validation in `QueryCommand`

**Add a new CLI option:**
1. Define option in `Program.cs`
2. Add to rootCommand
3. Wire in the layering section (env var line if applicable, CLI override line)
4. Thread through to `QueryCommand.Execute`
