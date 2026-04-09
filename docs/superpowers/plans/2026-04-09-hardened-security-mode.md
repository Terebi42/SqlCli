# Hardened Security Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a compile-time `SecurityMode` MSBuild property (Standard/Hardened) that prevents AI agents from escalating SQL permissions by modifying the config file.

**Architecture:** A `Directory.Build.props` at the solution root validates `SecurityMode` and propagates a `HARDENED_SECURITY` define. `ConfigLoader` is refactored to separate config auto-generation, file-based loading, and hardened loading into distinct internal methods. A single `#if` dispatcher in `LoadSecurity` selects the path; a second `#if` sets an `IsHardened` const for runtime inspection by `AgentHelp`.

**Tech Stack:** .NET 10, MSBuild, C# preprocessor directives, MSTest

**User Verification:** NO

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Directory.Build.props` | Create | SecurityMode validation + HARDENED_SECURITY define |
| `src/SqlCli/Config/ConfigLoader.cs` | Modify | Extract EnsureConfigExists, CreateHardenedSecurity, LoadSecurityFromFile; add #if dispatcher and IsHardened const |
| `src/SqlCli/Config/SqlCliConfig.cs` | Modify | Update security section ConfigComment |
| `src/SqlCli/Program.cs` | Modify | Call EnsureConfigExists before Load* methods |
| `src/SqlCli/AgentHelp.cs` | Modify | Output security mode |
| `tests/SqlCli.Tests/Config/ConfigLoaderTests.cs` | Modify | Update existing tests for refactored methods, add new tests |
| `.github/workflows/build.yml` | Modify | Add -p:SecurityMode=Standard |
| `README.md` | Modify | Document SecurityMode build property |

---

### Task 1: Create Directory.Build.props

**Goal:** Add MSBuild plumbing that validates SecurityMode and propagates HARDENED_SECURITY define to all projects.

**Files:**
- Create: `Directory.Build.props`

**Acceptance Criteria:**
- [ ] Build with `-p:SecurityMode=Standard` succeeds
- [ ] Build with `-p:SecurityMode=Hardened` succeeds
- [ ] Build without SecurityMode fails with descriptive error
- [ ] Build with `-p:SecurityMode=Invalid` fails with descriptive error
- [ ] HARDENED_SECURITY is defined when SecurityMode=Hardened
- [ ] HARDENED_SECURITY is not defined when SecurityMode=Standard

**Verify:** `dotnet build SqlCli.slnx -p:SecurityMode=Standard` → succeeds; `dotnet build SqlCli.slnx` → error mentioning SecurityMode

**Steps:**

- [ ] **Step 1: Create Directory.Build.props**

Create `Directory.Build.props` in the solution root:

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

- [ ] **Step 2: Verify Standard mode builds**

Run: `dotnet build SqlCli.slnx -p:SecurityMode=Standard`
Expected: Build succeeds

- [ ] **Step 3: Verify Hardened mode builds**

Run: `dotnet build SqlCli.slnx -p:SecurityMode=Hardened`
Expected: Build succeeds

- [ ] **Step 4: Verify missing SecurityMode fails**

Run: `dotnet build SqlCli.slnx`
Expected: Build error containing "SecurityMode property is required"

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props
git commit -m "build: add SecurityMode MSBuild property with validation"
```

---

### Task 2: Refactor ConfigLoader and wire up Program.cs

**Goal:** Extract `EnsureConfigExists`, `CreateHardenedSecurity`, and `LoadSecurityFromFile` as internal methods; add `#if` dispatcher in `LoadSecurity`; add `IsHardened` const; call `EnsureConfigExists` from `Program.cs`.

**Files:**
- Modify: `src/SqlCli/Config/ConfigLoader.cs:55-82`
- Modify: `src/SqlCli/Program.cs:106-115`

**Acceptance Criteria:**
- [ ] `EnsureConfigExists` creates default config when no file exists
- [ ] `EnsureConfigExists` is a no-op when config already exists
- [ ] `LoadSecurityFromFile` loads from combined config
- [ ] `LoadSecurityFromFile` loads from dedicated security file
- [ ] `LoadSecurityFromFile` returns safe defaults when no file found
- [ ] `CreateHardenedSecurity` returns SelectStatement-only config
- [ ] `LoadSecurity` dispatches to `CreateHardenedSecurity` under HARDENED_SECURITY
- [ ] `LoadSecurity` dispatches to `LoadSecurityFromFile` without HARDENED_SECURITY
- [ ] `IsHardened` is true when compiled with HARDENED_SECURITY, false otherwise
- [ ] `Program.cs` calls `EnsureConfigExists` before any `Load*` calls

**Verify:** `dotnet build SqlCli.slnx -p:SecurityMode=Standard` → succeeds; `dotnet test SqlCli.slnx -p:SecurityMode=Standard` → all tests pass

**Steps:**

- [ ] **Step 1: Add IsHardened const to ConfigLoader**

Add at the top of the `ConfigLoader` class, after the existing file name constants:

```csharp
/// <summary>
/// Indicates whether this binary was compiled in Hardened security mode.
/// When true, security settings are compiled into the binary and config file security section is ignored.
/// </summary>
#if HARDENED_SECURITY
internal const bool IsHardened = true;
#else
internal const bool IsHardened = false;
#endif
```

- [ ] **Step 2: Extract EnsureConfigExists**

Add a new public static method to `ConfigLoader`, before `LoadSecurity`:

```csharp
/// <summary>
/// Ensures a config file exists in the executable directory.
/// If neither the combined config nor the dedicated security file exists,
/// generates a default combined config with <c>["SelectStatement"]</c>.
/// This is an operational convenience and runs in both Standard and Hardened modes.
/// </summary>
/// <param name="exeDir">Directory containing the executable.</param>
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

- [ ] **Step 3: Extract CreateHardenedSecurity**

Add a new internal static method to `ConfigLoader`:

```csharp
/// <summary>
/// Returns the hardcoded security configuration for Hardened builds.
/// Allows only SelectStatement with no dangerous select features.
/// </summary>
/// <returns>Hardcoded immutable security configuration.</returns>
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
```

- [ ] **Step 4: Extract LoadSecurityFromFile**

Add a new internal static method to `ConfigLoader` containing the existing file-loading logic from `LoadSecurity`, minus the auto-generation (now in `EnsureConfigExists`):

```csharp
/// <summary>
/// Loads security configuration from config files in the executable directory.
/// Checks for a dedicated security file first, then falls back to the combined config.
/// If neither exists, returns a safe default (SelectStatement only).
/// </summary>
/// <param name="exeDir">Directory containing the executable and config files.</param>
/// <returns>Immutable security configuration.</returns>
internal static SecurityConfig LoadSecurityFromFile( string exeDir )
{
    var securityPath = Path.Combine( exeDir, SecurityFileName );
    var configPath = Path.Combine( exeDir, ConfigFileName );

    // Try dedicated security file first (takes precedence)
    if ( File.Exists( securityPath ) )
    {
        var config = DeserializeFile( securityPath );
        ValidateFilterMode( config.Security.FilterMode );
        return config.Security;
    }

    // Fall back to combined config file
    if ( File.Exists( configPath ) )
    {
        var config = DeserializeFile( configPath );
        ValidateFilterMode( config.Security.FilterMode );
        return config.Security;
    }

    // EnsureConfigExists should have created it, but defensive fallback
    return new SecurityConfig
    {
        AllowedStatements = new List<string> { "SelectStatement" }
    };
}
```

- [ ] **Step 5: Replace LoadSecurity body with #if dispatcher**

Replace the entire body of the existing `LoadSecurity` method:

```csharp
/// <summary>
/// Loads the immutable security configuration.
/// In Hardened builds, returns compiled-in defaults (config file ignored).
/// In Standard builds, loads from the executable directory config files.
/// </summary>
/// <param name="exeDir">Directory containing the executable and config files.</param>
/// <returns>Immutable security configuration.</returns>
public static SecurityConfig LoadSecurity( string exeDir )
{
#if HARDENED_SECURITY
    return CreateHardenedSecurity();
#else
    return LoadSecurityFromFile( exeDir );
#endif
}
```

- [ ] **Step 6: Wire EnsureConfigExists in Program.cs**

In `Program.cs`, add `EnsureConfigExists` call immediately before the `// Load security config` comment block (before line 105):

```csharp
// Ensure config file exists (auto-generate if needed) — runs in both modes
try
{
    ConfigLoader.EnsureConfigExists( AppContext.BaseDirectory );
}
catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException )
{
    // Non-fatal — config generation is a convenience, not a requirement
    Console.Error.WriteLine( $"WARNING: Could not auto-generate config: {ex.Message}" );
}
```

- [ ] **Step 7: Build and run existing tests**

Run: `dotnet build SqlCli.slnx -p:SecurityMode=Standard`
Run: `dotnet test SqlCli.slnx -p:SecurityMode=Standard`
Expected: All existing tests pass (some may need adjustment in Task 3)

- [ ] **Step 8: Commit**

```bash
git add src/SqlCli/Config/ConfigLoader.cs src/SqlCli/Program.cs
git commit -m "refactor: extract ConfigLoader methods for hardened security mode"
```

---

### Task 3: Update tests for refactored ConfigLoader

**Goal:** Update existing tests to use the new internal methods and add new tests for `CreateHardenedSecurity`, `LoadSecurityFromFile`, and `EnsureConfigExists`.

**Files:**
- Modify: `tests/SqlCli.Tests/Config/ConfigLoaderTests.cs`

**Acceptance Criteria:**
- [ ] All existing test scenarios still covered
- [ ] `CreateHardenedSecurity` returns expected values
- [ ] `LoadSecurityFromFile` loads from combined config
- [ ] `LoadSecurityFromFile` loads from dedicated security file
- [ ] `LoadSecurityFromFile` returns safe defaults when no file found
- [ ] `EnsureConfigExists` creates config when no file exists
- [ ] `EnsureConfigExists` is a no-op when combined config exists
- [ ] `EnsureConfigExists` is a no-op when security file exists

**Verify:** `dotnet test SqlCli.slnx -p:SecurityMode=Standard` → all tests pass

**Steps:**

- [ ] **Step 1: Update LoadSecurity_MissingFile test to use EnsureConfigExists**

The test `LoadSecurity_MissingFile_GeneratesDefaultWithSelectStatement` currently expects `LoadSecurity` to auto-generate the config. Now that's in `EnsureConfigExists`. Update:

```csharp
[TestMethod]
public void LoadSecurity_MissingFile_GeneratesDefaultWithSelectStatement()
{
    ConfigLoader.EnsureConfigExists( _tempDir );

    var security = ConfigLoader.LoadSecurity( _tempDir );

    Assert.AreEqual( "whitelist", security.FilterMode );
    CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
    Assert.IsTrue( security.Audit.Enabled );
    Assert.IsTrue( File.Exists( Path.Combine( _tempDir, "sqlcli.config.jsonc" ) ) );
}
```

- [ ] **Step 2: Add EnsureConfigExists tests**

```csharp
[TestMethod]
public void EnsureConfigExists_NoFiles_CreatesDefaultConfig()
{
    ConfigLoader.EnsureConfigExists( _tempDir );

    var configPath = Path.Combine( _tempDir, "sqlcli.config.jsonc" );
    Assert.IsTrue( File.Exists( configPath ) );

    var content = File.ReadAllText( configPath );
    StringAssert.Contains( content, "SelectStatement" );
}

[TestMethod]
public void EnsureConfigExists_CombinedConfigExists_DoesNotOverwrite()
{
    var configPath = Path.Combine( _tempDir, "sqlcli.config.jsonc" );
    var original = """{ "security": { "allowedStatements": ["ExecuteStatement"] } }""";
    File.WriteAllText( configPath, original );

    ConfigLoader.EnsureConfigExists( _tempDir );

    var content = File.ReadAllText( configPath );
    Assert.AreEqual( original, content );
}

[TestMethod]
public void EnsureConfigExists_SecurityFileExists_DoesNotCreateCombined()
{
    var securityPath = Path.Combine( _tempDir, "sqlcli.security.jsonc" );
    File.WriteAllText( securityPath, """{ "security": { "allowedStatements": ["SelectStatement"] } }""" );

    ConfigLoader.EnsureConfigExists( _tempDir );

    Assert.IsFalse( File.Exists( Path.Combine( _tempDir, "sqlcli.config.jsonc" ) ) );
}
```

- [ ] **Step 3: Add CreateHardenedSecurity tests**

```csharp
[TestMethod]
public void CreateHardenedSecurity_ReturnsSelectStatementOnly()
{
    var security = ConfigLoader.CreateHardenedSecurity();

    Assert.AreEqual( "whitelist", security.FilterMode );
    Assert.AreEqual( 1, security.AllowedStatements.Count );
    Assert.AreEqual( "SelectStatement", security.AllowedStatements[0] );
    Assert.AreEqual( 0, security.AllowedSelectFeatures.Count );
    Assert.IsTrue( security.Audit.Enabled );
    Assert.AreEqual( "sqlcli-audit.log", security.Audit.Path );
}
```

- [ ] **Step 4: Add LoadSecurityFromFile tests**

```csharp
[TestMethod]
public void LoadSecurityFromFile_ValidCombinedConfig_LoadsCorrectly()
{
    var json = """
    {
      "security": {
        "filterMode": "whitelist",
        "allowedStatements": ["SelectStatement", "ExecuteStatement"],
        "audit": { "enabled": false, "path": "custom.log" }
      }
    }
    """;
    File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

    var security = ConfigLoader.LoadSecurityFromFile( _tempDir );

    Assert.AreEqual( "whitelist", security.FilterMode );
    CollectionAssert.AreEqual( new[] { "SelectStatement", "ExecuteStatement" }, security.AllowedStatements.ToArray() );
    Assert.IsFalse( security.Audit.Enabled );
    Assert.AreEqual( "custom.log", security.Audit.Path );
}

[TestMethod]
public void LoadSecurityFromFile_SecurityFileExists_TakesPrecedence()
{
    var combinedJson = """
    {
      "security": {
        "filterMode": "whitelist",
        "allowedStatements": ["SelectStatement", "ExecuteStatement"]
      }
    }
    """;
    var securityJson = """
    {
      "security": {
        "filterMode": "whitelist",
        "allowedStatements": ["SelectStatement"]
      }
    }
    """;
    File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), combinedJson );
    File.WriteAllText( Path.Combine( _tempDir, "sqlcli.security.jsonc" ), securityJson );

    var security = ConfigLoader.LoadSecurityFromFile( _tempDir );

    CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
}

[TestMethod]
public void LoadSecurityFromFile_NoFiles_ReturnsSafeDefaults()
{
    var security = ConfigLoader.LoadSecurityFromFile( _tempDir );

    Assert.AreEqual( "whitelist", security.FilterMode );
    CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
}
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test SqlCli.slnx -p:SecurityMode=Standard`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add tests/SqlCli.Tests/Config/ConfigLoaderTests.cs
git commit -m "test: add tests for EnsureConfigExists, CreateHardenedSecurity, LoadSecurityFromFile"
```

---

### Task 4: Update config comment and AgentHelp

**Goal:** Update the security section comment in `SqlCliConfig.cs` to explain both modes, and update `AgentHelp.cs` to output the current security mode.

**Files:**
- Modify: `src/SqlCli/Config/SqlCliConfig.cs:27`
- Modify: `src/SqlCli/AgentHelp.cs`

**Acceptance Criteria:**
- [ ] Generated config includes Standard/Hardened mode explanation in security section comment
- [ ] `--agent-help` output includes current security mode
- [ ] Agent help explains what the mode means for config file behavior

**Verify:** `dotnet test SqlCli.slnx -p:SecurityMode=Standard` → all tests pass

**Steps:**

- [ ] **Step 1: Update security section ConfigComment in SqlCliConfig.cs**

Replace the `[ConfigComment]` on the `Security` property (line 27):

```csharp
[ConfigComment( "Security settings — statement filtering, audit logging.\nIn Standard builds, these settings are loaded from this file.\nIn Hardened builds, these settings are compiled into the binary and this section is ignored.\nThese settings cannot be overridden via CLI arguments or environment variables." )]
```

- [ ] **Step 2: Update AgentHelp.cs to show security mode**

Add after the `**Binary:**` line (around line 24):

```csharp
**Security Mode:** {{( ConfigLoader.IsHardened ? "Hardened (security whitelist compiled into binary, config file security section ignored)" : "Standard (security whitelist loaded from config file)" )}}
```

- [ ] **Step 3: Update the Safety Model section in AgentHelp**

In the Safety Model section, update the bullet about the whitelist source (around line 49):

```csharp
- The whitelist is {{( ConfigLoader.IsHardened ? "compiled into this binary and cannot be changed without rebuilding from source" : "defined in `sqlcli.config.jsonc` next to the executable (not overridable via CLI)" )}}
```

- [ ] **Step 4: Add using directive in AgentHelp.cs**

Add at the top of `AgentHelp.cs`:

```csharp
using SqlCli.Config;
```

- [ ] **Step 5: Verify generated config includes mode comment**

Run: `dotnet test SqlCli.slnx -p:SecurityMode=Standard`
Expected: `JsoncGenerator_IncludesComments` test still passes, and security section now includes "Standard builds" and "Hardened builds" in the output.

- [ ] **Step 6: Commit**

```bash
git add src/SqlCli/Config/SqlCliConfig.cs src/SqlCli/AgentHelp.cs
git commit -m "feat: show security mode in agent help and config comments"
```

---

### Task 5: Update CI and README

**Goal:** Add `-p:SecurityMode=Standard` to CI build/test steps and document SecurityMode in README.

**Files:**
- Modify: `.github/workflows/build.yml:32-36`
- Modify: `README.md`

**Acceptance Criteria:**
- [ ] CI build step passes SecurityMode=Standard
- [ ] CI test step passes SecurityMode=Standard
- [ ] README documents both security modes
- [ ] README build instructions include SecurityMode

**Verify:** `dotnet build SqlCli.slnx -p:SecurityMode=Standard` → succeeds

**Steps:**

- [ ] **Step 1: Update build.yml**

Update the Build step (line 32):

```yaml
    - name: Build
      run: dotnet build SqlCli.slnx --no-restore --configuration Release -p:SecurityMode=Standard
```

Update the Test step (line 35):

```yaml
    - name: Test
      run: dotnet test SqlCli.slnx --no-build --configuration Release --verbosity normal -p:SecurityMode=Standard
```

- [ ] **Step 2: Add Security Mode section to README**

Add a new section after the "## Security Model" section and before "## Limitations":

```markdown
## Security Mode (Build-Time)

SqlCli must be built with an explicit `SecurityMode` property. The build fails if this is not set, ensuring a conscious choice.

### Standard Mode

```bash
dotnet build SqlCli.slnx -p:SecurityMode=Standard
```

Security whitelists are loaded from `sqlcli.config.jsonc` at runtime. This is the default for development and flexible deployments where an administrator manages the config file.

### Hardened Mode

```bash
dotnet build SqlCli.slnx -p:SecurityMode=Hardened
```

Security whitelists are compiled into the binary. The config file's security section is ignored. This prevents an AI agent (or any process with file system access) from escalating its SQL permissions by modifying the config file before invoking SqlCli.

Hardened mode compiles in:
- `AllowedStatements`: `["SelectStatement"]`
- `AllowedSelectFeatures`: `[]` (all dangerous features blocked)
- Audit logging enabled

Operational and app settings (timeouts, server, database, etc.) are still loaded from config files and CLI args in both modes.
```

- [ ] **Step 3: Update Building section in README**

Replace the existing Building section (lines 198-200):

```markdown
## Building

SecurityMode is required. Choose Standard (config-file whitelists) or Hardened (compiled-in whitelists):

```bash
dotnet build SqlCli.slnx -p:SecurityMode=Standard
dotnet build SqlCli.slnx -p:SecurityMode=Hardened
```

## Testing

```bash
dotnet test SqlCli.slnx -p:SecurityMode=Standard
```
```

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/build.yml README.md
git commit -m "docs: document SecurityMode property in README and CI"
```

---

### Task 6: Final verification

**Goal:** Verify the complete feature works end-to-end in both modes.

**Files:**
- No file changes — verification only

**Acceptance Criteria:**
- [ ] `dotnet build SqlCli.slnx -p:SecurityMode=Standard` succeeds
- [ ] `dotnet build SqlCli.slnx -p:SecurityMode=Hardened` succeeds
- [ ] `dotnet build SqlCli.slnx` fails with descriptive error
- [ ] `dotnet test SqlCli.slnx -p:SecurityMode=Standard` — all tests pass
- [ ] `dotnet test SqlCli.slnx -p:SecurityMode=Hardened` — all tests pass

**Verify:** All commands above produce expected results

**Steps:**

- [ ] **Step 1: Clean build Standard**

Run: `dotnet clean SqlCli.slnx`
Run: `dotnet build SqlCli.slnx -p:SecurityMode=Standard`
Expected: Build succeeds

- [ ] **Step 2: Test Standard**

Run: `dotnet test SqlCli.slnx -p:SecurityMode=Standard`
Expected: All tests pass

- [ ] **Step 3: Clean build Hardened**

Run: `dotnet clean SqlCli.slnx`
Run: `dotnet build SqlCli.slnx -p:SecurityMode=Hardened`
Expected: Build succeeds

- [ ] **Step 4: Test Hardened**

Run: `dotnet test SqlCli.slnx -p:SecurityMode=Hardened`
Expected: All tests pass

- [ ] **Step 5: Verify missing SecurityMode fails**

Run: `dotnet build SqlCli.slnx`
Expected: Build error containing "SecurityMode property is required"

- [ ] **Step 6: Verify generated config includes mode comments**

Run the Standard-built binary with `--generate-config` in a temp dir and check the output contains "In Standard builds" and "In Hardened builds" in the security section comments.
