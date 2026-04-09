# Hardened Security Mode

## Problem

SqlCli's security whitelists (allowed statement types, allowed SELECT features) are loaded from a JSONC config file at the executable directory. An AI agent with file system access could modify this config file before invoking SqlCli, escalating its own permissions (e.g., adding `ExecuteStatement` to enable arbitrary SQL execution).

## Solution

Add a compile-time `SecurityMode` MSBuild property that forces an explicit choice between two modes:

- **Standard** — current behavior, whitelists loaded from config file
- **Hardened** — whitelists compiled into the binary, config file security section ignored

The build fails if `SecurityMode` is not set, ensuring a conscious choice.

## Design

### MSBuild: Directory.Build.props

A `Directory.Build.props` at the solution root handles all MSBuild plumbing for both the main project and test project:

- Validates that `SecurityMode` is set to `Standard` or `Hardened`, with a helpful error message explaining each mode
- When `Hardened`, adds `HARDENED_SECURITY` to `DefineConstants`

```xml
<Project>
  <PropertyGroup Condition="'$(SecurityMode)' == 'Hardened'">
    <DefineConstants>$(DefineConstants);HARDENED_SECURITY</DefineConstants>
  </PropertyGroup>

  <Target Name="_ValidateSecurityMode" BeforeTargets="CoreCompile">
    <Error Condition="'$(SecurityMode)' != 'Standard' and '$(SecurityMode)' != 'Hardened'"
           Text="SecurityMode property is required. Use -p:SecurityMode=Standard (security whitelist loaded from config file) or -p:SecurityMode=Hardened (security whitelist compiled into binary, config file ignored). See README.md for details." />
  </Target>
</Project>
```

The existing `SqlCli.csproj` and `SqlCli.Tests.csproj` do not need SecurityMode-related changes — they inherit from `Directory.Build.props`.

### ConfigLoader Refactoring

Three changes to `ConfigLoader.cs`:

#### 1. Extract `EnsureConfigExists`

The current `LoadSecurity` has a side effect: if no config file exists, it generates a default one. This is operational convenience, not a security concern. Extract it so it runs in both modes.

```csharp
public static void EnsureConfigExists( string exeDir )
{
    var configPath = Path.Combine( exeDir, ConfigFileName );
    if ( !File.Exists( configPath ) && !File.Exists( Path.Combine( exeDir, SecurityFileName ) ) )
    {
        var defaultConfig = CreateDefaultConfig();
        var jsonc = JsoncGenerator.Generate( defaultConfig );
        File.WriteAllText( configPath, jsonc );
        Console.Error.WriteLine( $"Config file not found. Created default config at: {configPath}" );
    }
}
```

Called from `Program.cs` before any `Load*` calls, in both modes.

#### 2. Extract `CreateHardenedSecurity` and `LoadSecurityFromFile`

Both methods are `internal`, always compiled, and independently testable:

```csharp
internal static SecurityConfig CreateHardenedSecurity()
{
    return new SecurityConfig
    {
        FilterMode = "whitelist",
        AllowedStatements = new List<string> { "SelectStatement" },
        AllowedSelectFeatures = new(),
        Audit = new AuditConfig()
    };
}

internal static SecurityConfig LoadSecurityFromFile( string exeDir )
{
    // Existing file-loading logic from LoadSecurity, minus the auto-generation
    // (now handled by EnsureConfigExists).
    // If no file is found (EnsureConfigExists should have created one, but defensively),
    // returns a safe default: SelectStatement only, no select features.
}
```

#### 3. `LoadSecurity` becomes a one-line dispatcher

```csharp
public static SecurityConfig LoadSecurity( string exeDir )
{
#if HARDENED_SECURITY
    return CreateHardenedSecurity();
#else
    return LoadSecurityFromFile( exeDir );
#endif
}
```

#### 4. `IsHardened` const

A compile-time const for runtime inspection (used by `AgentHelp`):

```csharp
#if HARDENED_SECURITY
internal const bool IsHardened = true;
#else
internal const bool IsHardened = false;
#endif
```

This is the second and final `#if` site in the codebase.

### Program.cs

Add `EnsureConfigExists` call before any config loading:

```csharp
// Ensure config file exists (auto-generate if needed) — runs in both modes
ConfigLoader.EnsureConfigExists( AppContext.BaseDirectory );
```

No other changes. The existing flow that calls `LoadSecurity`, `LoadOperational`, `LoadApp` and passes `SecurityConfig` downstream is unchanged.

### Generated Config Comments (SqlCliConfig.cs)

Update the `[ConfigComment]` on the `Security` property to explain both modes. The same comment is emitted regardless of build mode:

```
Security settings — statement filtering, audit logging.
In Standard builds, these settings are loaded from this file.
In Hardened builds, these settings are compiled into the binary and this section is ignored.
These settings cannot be overridden via CLI arguments or environment variables.
```

No changes to `JsoncGenerator.cs` — the existing reflection-based emission handles it.

### Hardened Values

When `SecurityMode=Hardened`, the compiled-in security config is:

- `FilterMode`: `"whitelist"`
- `AllowedStatements`: `["SelectStatement"]`
- `AllowedSelectFeatures`: `[]` (empty — all dangerous features blocked)
- `Audit`: enabled, default path `"sqlcli-audit.log"`

These match the current shipped defaults from `CreateDefaultConfig`.

### Downstream Impact

None. Everything downstream of `ConfigLoader.LoadSecurity` — `ScriptDomStatementFilter`, `QueryCommand`, `Program.cs` — consumes a `SecurityConfig` instance and doesn't care where it came from. No changes needed.

### Tests

Both `CreateHardenedSecurity()` and `LoadSecurityFromFile()` are `internal` and always compiled, so both are testable in a single test run regardless of build mode. `InternalsVisibleTo` is already configured.

**New tests:**

- `CreateHardenedSecurity` returns expected values (SelectStatement only, no select features, audit enabled)
- `LoadSecurityFromFile` loads from combined config file
- `LoadSecurityFromFile` loads from dedicated security file (split layout)
- `LoadSecurityFromFile` returns safe defaults when file exists but has no security section
- `EnsureConfigExists` creates config when no file exists
- `EnsureConfigExists` is a no-op when config already exists

**Modified tests:**

- Existing `ConfigLoaderTests` that tested the "no file exists, auto-generate" behavior of `LoadSecurity` — update to call `EnsureConfigExists` first, then `LoadSecurityFromFile`.

**No `#if` in test code.** No CI matrix needed — a single `dotnet test -p:SecurityMode=Standard` run covers both paths.

### CI (build.yml)

Add `-p:SecurityMode=Standard` to build and test steps:

```yaml
- name: Build
  run: dotnet build SqlCli.slnx --no-restore --configuration Release -p:SecurityMode=Standard

- name: Test
  run: dotnet test SqlCli.slnx --no-build --configuration Release --verbosity normal -p:SecurityMode=Standard
```

### Documentation

**README.md:**
- Document the `SecurityMode` property
- Build instructions for both modes
- Explain the threat model and when to use Hardened

**AgentHelp.cs:**
- Output the current security mode using `ConfigLoader.IsHardened`
- Explain what it means so agents understand whether config file edits have any effect

### `#if` Inventory

Exactly two `#if HARDENED_SECURITY` sites in the entire codebase, both in `ConfigLoader.cs`:

1. `LoadSecurity` — one-line dispatcher
2. `IsHardened` — one-line const

Everything else is unconditionally compiled.
