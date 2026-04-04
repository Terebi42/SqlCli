# Contributing to SqlCli

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows)
- Git

## Build and Test

```bash
dotnet build SqlCli.slnx
dotnet test SqlCli.slnx
```

## Project Structure

```
SqlCli.slnx                         Solution file
src/SqlCli/                          Main project
  Program.cs                         Entry point, CLI option definitions, Option D layering
  ExitCode.cs                        Flags enum exit codes
  AgentHelp.cs                       --agent-help output text
  Auth/
    AuthMode.cs                      Auth mode resolution (domain/windows/sql)
    ConnectionStringBuilder.cs       Builds SQL connection strings from config
    SspiContextProvider.cs           SSPI/NegotiateAuthentication for domain auth
  Commands/
    QueryCommand.cs                  Main execution pipeline
  Config/
    SqlCliConfig.cs                  Combined config model (has Security, Operational, App children)
    SecurityConfig.cs                Immutable security settings (whitelist, audit)
    OperationalConfig.cs             Mutable operational settings (timeouts, limits, format)
    AppConfig.cs                     Mutable app/environment settings (server, database)
    ConfigLoader.cs                  Three-layer config loading with JsonElement-based layering
    ConfigCommentAttribute.cs        Attributes for JSONC generator
    JsoncGenerator.cs                Reflection-based JSONC config file generator
  Execution/
    QueryExecutor.cs                 Runs SQL via SqlCommand, enforces maxRows
    QueryResult.cs                   Result set model
    MaxRowsExceededException.cs      Thrown when row limit exceeded
  Filtering/
    IStatementFilter.cs              Filter interface
    FilterResult.cs                  Filter result with violations and parsed statements
    ScriptDomStatementFilter.cs      AST-based filter with DangerousSelectFeatureVisitor
  Output/
    IResultFormatter.cs              Formatter interface + OutputFormatException
    CsvFormatter.cs                  CSV output via CsvHelper
    JsonFormatter.cs                 JSON output via System.Text.Json
    TableFormatter.cs                Human-readable table output
  sqlcli.config.jsonc                Default config (shipped with binary)
tests/SqlCli.Tests/                  Test project (MSTest + NSubstitute)
```

## Coding Standards

The project follows these conventions (enforced by `.editorconfig`):

- **Tabs** for indentation
- **Block-scoped namespaces** (not file-scoped)
- **Spaces inside parentheses** for method calls, declarations, and control flow
- **Allman brace style** (new line before opening brace)
- **`var` everywhere** (no explicit types)
- **XML doc comments** on all public, internal, and private members
- **Always use braces** (no single-line if/foreach)
- **Explicit usings** (ImplicitUsings is disabled)

## Security Model

The security model is the most important thing to understand before contributing.

**The filter is defense-in-depth, not a complete security boundary.** Server-side permissions (e.g., `db_datareader`) are the primary guardrail. The filter catches structural dangers that the parser can see.

### Trust Boundaries

| Config Layer | Source | Overridable | Controls |
|---|---|---|---|
| **SecurityConfig** | File only (exe dir) | Never | Whitelist, audit |
| **OperationalConfig** | File + CLI | Yes | Timeouts, limits, format |
| **AppConfig** | File + CLI | Yes | Server, database, TLS |

**SecurityConfig is immutable at runtime.** No CLI arg, env var, or working-directory config can modify it.

### When Changing Filters

If you modify `ScriptDomStatementFilter` or the `DangerousSelectFeatureVisitor`:

1. Consider whether a new SQL construct could bypass the whitelist
2. Add it to the visitor if it's catchable at the AST level
3. Make it opt-in via `allowedSelectFeatures` if there are legitimate use cases
4. Add tests for both the blocked and allowed cases
5. Update `AgentHelp.cs`, `README.md`, and config comments
6. Get a security-focused review

## Adding Features

### New Statement Type

1. The type already exists in ScriptDom -- just add its name to `allowedStatements` in the config
2. Update the config comment stubs in `SqlCliConfig.cs` `[ConfigComment]` attribute
3. Update `AgentHelp.cs` and `README.md` tables

### New Dangerous SELECT Feature

1. Add a `Has*` property to `DangerousSelectFeatureVisitor`
2. Add an `ExplicitVisit` override for the ScriptDom node type
3. Add a check in `CheckSelectFeatures` with an `allowedSelectFeatures` key
4. Add the key to config comments in `SecurityConfig.cs`
5. Update `AgentHelp.cs` and `README.md`
6. Add tests: blocked by default + passes when feature allowed

### New Output Format

1. Create a new class implementing `IResultFormatter`
2. Add it to the switch in `QueryCommand.Execute`
3. Add tests
4. Update help text

### New Config Property

1. Add the property to the appropriate config class (`SecurityConfig`, `OperationalConfig`, or `AppConfig`)
2. Add `[ConfigComment]` attribute with documentation
3. If operational/app: add `LayerFromJson` entry in `ConfigLoader.cs`
4. If operational/app: add CLI option in `Program.cs` and wire it in the layering section
5. The JSONC generator will automatically include it in generated config files

## Pull Requests

- All tests must pass (`dotnet test SqlCli.slnx`)
- New functionality needs test coverage
- Security-related changes need explicit review
- Update documentation (README, AgentHelp, config comments) for user-facing changes

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
