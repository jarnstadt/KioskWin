# KioskWin Implementation Plan

> Historical plan: password hashing and related configuration in this document have been superseded. See the README for current configuration and authentication behavior.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full-screen WinForms + WebView2 kiosk application that loads a configurable remote page and prevents regular users from closing it or switching it to the background.

**Architecture:** A single WinExe project (`KioskWin`, net8.0-windows) plus an xUnit test project. Put pure logic in `Core/` (configuration, password hashing, retry state machine, logging, key-combination parsing) and cover it with TDD; put Win32/WebView2/window behavior in `Forms/` and cover it with a manual QA checklist. `Program.cs` handles the entry point, single-instance behavior, exception handling, and `--hash-password` CLI. Publish as a self-contained single file that can be copied and run.

**Tech Stack:** C# / .NET 8 (net8.0-windows), WinForms, Microsoft.Web.WebView2 (Evergreen), Microsoft.Extensions.Configuration, xUnit.

## Global Constraints

- **Target framework**: `net8.0-windows`. Build, publish, and test on Windows (WSL has no dotnet; source is at `/mnt/d/code/position-win`, the Windows `D:` drive; edit in WSL and compile on Windows).
- **NuGet versions**: `Microsoft.Web.WebView2` 1.0.2903.40; `Microsoft.Extensions.Configuration.Json` 8.0.1; `Microsoft.Extensions.Configuration.Binder` 8.0.2; for tests, `Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2, and `Microsoft.Extensions.Configuration.Memory` 8.0.1. If a version cannot be restored, use the latest patch version in the same major release.
- **Namespaces**: main project `KioskWin`, `KioskWin.Core`, `KioskWin.Forms`; tests `KioskWin.Tests`.
- **UI construction**: Do not use `.Designer.cs` (construct all controls in code, in the constructors) so the code can be presented completely without the WinForms designer.
- **Configuration file**: Keep `appsettings.json` next to the exe and set `CopyToOutputDirectory=PreserveNewest`. **Never store plaintext passwords**—store `AdminPasswordHash` (SHA-256 hex) + a random `PasswordSalt`.
- **Single-instance mutex name**: `Global\KioskWin_SingleInstance`.
- **Log directory**: `%LocalAppData%\KioskWin\logs\`, with daily `yyyy-MM-dd.log` files.
- **Commit after every task** (frequent commits). The repository is initialized at the project root.
- **YAGNI**: implement only the requirements in section 2 of the specification; do not implement medium/high-security lockdown (section 12).

---

## File structure (files created/modified by this plan)

| File | Responsibility | Task |
|---|---|---|
| `KioskWin.sln` | Solution | Task 1 |
| `src/KioskWin/KioskWin.csproj` | WinForms project + dependencies | Task 1 |
| `src/KioskWin/Program.cs` | Entry point (placeholder → final) | Task 1 / Task 10 |
| `src/KioskWin/appsettings.json` | Configuration | Task 1 |
| `src/KioskWin/Core/FileLogger.cs` | Daily file logging | Task 2 |
| `src/KioskWin/Core/PasswordHasher.cs` | SHA-256(salt+pwd) hashing/verification | Task 3 |
| `src/KioskWin/Core/KeyCombinationParser.cs` | "Ctrl+Shift+Alt+Q" → Keys | Task 4 |
| `src/KioskWin/Core/KioskConfig.cs` | Configuration POCO + loading/validation/defaults | Task 5 |
| `src/KioskWin/Core/RetryController.cs` | Retry state machine + IRetryScheduler | Task 6 |
| `src/KioskWin/Core/FormsRetryScheduler.cs` | Production Forms.Timer scheduler | Task 6 |
| `src/KioskWin/Forms/RetryOverlay.cs` | Reusable full-screen overlay control | Task 7 |
| `src/KioskWin/Forms/AdminDialog.cs` | Password field + action-button dialog | Task 8 |
| `src/KioskWin/Forms/MainForm.cs` | Borderless full-screen + WebView2 + lockdown + retry + admin entry | Task 9 |
| `src/KioskWin/install-shortcut.ps1` | Startup shortcut | Task 11 |
| `tests/KioskWin.Tests/KioskWin.Tests.csproj` | Test project | Task 1 |
| `tests/KioskWin.Tests/*.cs` | Core unit tests | Tasks 2–6 |

---

## Task 1: Project scaffolding and build verification

**Files:**
- Create: `KioskWin.sln`
- Create: `src/KioskWin/KioskWin.csproj`
- Create: `src/KioskWin/Program.cs` (placeholder)
- Create: `src/KioskWin/appsettings.json`
- Create: `tests/KioskWin.Tests/KioskWin.Tests.csproj`
- Create: `tests/KioskWin.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: A buildable solution, the `KioskWin` namespace, and a base for files added in later tasks.

- [ ] **Step 1: Create the solution**

Run from the project root (`D:\code\position-win`, WSL path `/mnt/d/code/position-win`):

```bash
dotnet new sln -n KioskWin
```

Expected: `KioskWin.sln` is generated.

- [ ] **Step 2: Write the main project csproj**

Create `src/KioskWin/KioskWin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>KioskWin</RootNamespace>
    <AssemblyName>KioskWin</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2903.40" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.2" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the placeholder Program.cs**

Create `src/KioskWin/Program.cs` (Task 10 replaces it with the final version):

```csharp
namespace KioskWin;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form());
    }
}
```

- [ ] **Step 4: Write appsettings.json**

Create `src/KioskWin/appsettings.json` (leave the password hash empty for now; populate it during deployment with `--hash-password`):

```json
{
  "Url": "https://position.lmding.cn/public/wjbl/xcj/dist/index.html#/",
  "AdminKeyCombination": "Ctrl+Shift+Alt+Q",
  "AdminPasswordHash": "",
  "PasswordSalt": "",
  "RetryIntervalSeconds": 10,
  "TopMost": true,
  "ShowInTaskbar": false,
  "UserDataFolder": "WebView2Data"
}
```

- [ ] **Step 5: Write the test project csproj**

Create `tests/KioskWin.Tests/KioskWin.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="8.0.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\KioskWin\KioskWin.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: Write a smoke test**

Create `tests/KioskWin.Tests/SmokeTests.cs`:

```csharp
using Xunit;

namespace KioskWin.Tests;

public class SmokeTests
{
    [Fact]
    public void TestRunnerWorks()
    {
        // Only verify that the test runner and project reference work; replaced by real Core tests starting in Task 2.
        Assert.Equal(1, 1);
    }
}
```

- [ ] **Step 7: Add both projects to the solution**

```bash
dotnet sln add src/KioskWin/KioskWin.csproj
dotnet sln add tests/KioskWin.Tests/KioskWin.Tests.csproj
```

- [ ] **Step 8: Restore + build + test**

```bash
dotnet restore
dotnet build KioskWin.sln -c Debug
dotnet test
```

Expected: restore succeeds; build has 0 errors; 1 test passes.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "chore: scaffold KioskWin solution and test project"
```

---

## Task 2: FileLogger（Core，TDD）

**Files:**
- Create: `src/KioskWin/Core/FileLogger.cs`
- Create: `tests/KioskWin.Tests/FileLoggerTests.cs`

**Interfaces:**
- Produces: `KioskWin.Core.FileLogger`
  - `FileLogger()` — writes by default to `%LocalAppData%\KioskWin\logs\`
  - `FileLogger(string logDirectory)` — accepts an injected directory for tests
  - `void Log(string message)` — appends one timestamped line to `yyyy-MM-dd.log`

- [ ] **Step 1: Write a failing test**

Create `tests/KioskWin.Tests/FileLoggerTests.cs`:

```csharp
using KioskWin.Core;
using Xunit;

namespace KioskWin.Tests;

public class FileLoggerTests
{
    [Fact]
    public void Log_creates_dated_file_and_writes_message()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KioskWinTest_" + Guid.NewGuid().ToString("N"));
        var logger = new FileLogger(dir);

        logger.Log("hello world");

        var file = Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}.log");
        Assert.True(File.Exists(file));
        var content = File.ReadAllText(file);
        Assert.Contains("hello world", content);
    }

    [Fact]
    public void Log_appends_multiple_lines_to_same_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KioskWinTest_" + Guid.NewGuid().ToString("N"));
        var logger = new FileLogger(dir);

        logger.Log("line one");
        logger.Log("line two");

        var file = Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}.log");
        var lines = File.ReadAllLines(file);
        Assert.Equal(2, lines.Length);
        Assert.Contains("line one", lines[0]);
        Assert.Contains("line two", lines[1]);
    }
}
```

- [ ] **Step 2: Delete the smoke test and run tests to confirm failure**

The smoke test from Task 1 has served its purpose; delete it:

```bash
rm tests/KioskWin.Tests/SmokeTests.cs
dotnet test
```

Expected: compilation/tests fail with `FileLogger` undefined.

- [ ] **Step 3: Implement FileLogger**

Create `src/KioskWin/Core/FileLogger.cs`:

```csharp
namespace KioskWin.Core;

public sealed class FileLogger
{
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public FileLogger()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KioskWin", "logs"))
    {
    }

    public FileLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
        var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
        lock (_lock)
        {
            File.AppendAllText(path, line);
        }
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test --filter "FullyQualifiedName~FileLoggerTests"
```

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add FileLogger with daily file rotation"
```

---

## Task 3: PasswordHasher（Core，TDD）

**Files:**
- Create: `src/KioskWin/Core/PasswordHasher.cs`
- Create: `tests/KioskWin.Tests/PasswordHasherTests.cs`

**Interfaces:**
- Produces: `KioskWin.Core.PasswordHasher`
  - `record PasswordHashResult(string Hash, string Salt)`
  - `static PasswordHashResult Generate(string password)` — generates a random 16-byte salt and returns hex values
  - `static bool Verify(string password, string expectedHashHex, string saltHex)` — uses a constant-time comparison
- Algorithm: `SHA-256(UTF8(password) + salt)`, hex-encoded.

- [ ] **Step 1: Write a failing test**

Create `tests/KioskWin.Tests/PasswordHasherTests.cs`:

```csharp
using KioskWin.Core;
using Xunit;

namespace KioskWin.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_accepts_correct_password()
    {
        var result = PasswordHasher.Generate("s3cret!");
        Assert.True(PasswordHasher.Verify("s3cret!", result.Hash, result.Salt));
    }

    [Fact]
    public void Verify_rejects_wrong_password()
    {
        var result = PasswordHasher.Generate("s3cret!");
        Assert.False(PasswordHasher.Verify("wrong", result.Hash, result.Salt));
    }

    [Fact]
    public void Generate_produces_different_salt_each_call()
    {
        var a = PasswordHasher.Generate("same");
        var b = PasswordHasher.Generate("same");
        Assert.NotEqual(a.Salt, b.Salt);
        Assert.NotEqual(a.Hash, b.Hash);
    }

    [Fact]
    public void Verify_rejects_empty_hash_or_salt()
    {
        Assert.False(PasswordHasher.Verify("x", "", "salt"));
        Assert.False(PasswordHasher.Verify("x", "hash", ""));
    }

    [Fact]
    public void Verify_rejects_non_hex_input_without_throwing()
    {
        Assert.False(PasswordHasher.Verify("x", "not-hex!@#", "zzzz"));
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~PasswordHasherTests"
```

Expected: failure because `PasswordHasher` is undefined.

- [ ] **Step 3: Implement PasswordHasher**

Create `src/KioskWin/Core/PasswordHasher.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace KioskWin.Core;

public sealed record PasswordHashResult(string Hash, string Salt);

public static class PasswordHasher
{
    private const int SaltBytes = 16;

    public static PasswordHashResult Generate(string password)
    {
        Span<byte> salt = stackalloc byte[SaltBytes];
        RandomNumberGenerator.Fill(salt);
        var saltArray = salt.ToArray();
        return new PasswordHashResult(ComputeHash(password, saltArray), Convert.ToHexString(saltArray));
    }

    public static bool Verify(string password, string expectedHashHex, string saltHex)
    {
        if (string.IsNullOrEmpty(expectedHashHex) || string.IsNullOrEmpty(saltHex))
            return false;

        byte[] salt;
        try { salt = Convert.FromHexString(saltHex); }
        catch { return false; }

        var actualHashHex = ComputeHash(password, salt);

        if (actualHashHex.Length != expectedHashHex.Length)
            return false;

        var actual = Convert.FromHexString(actualHashHex);
        var expected = Convert.FromHexString(expectedHashHex);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string ComputeHash(string password, byte[] salt)
    {
        var pw = Encoding.UTF8.GetBytes(password);
        var buf = new byte[pw.Length + salt.Length];
        Buffer.BlockCopy(pw, 0, buf, 0, pw.Length);
        Buffer.BlockCopy(salt, 0, buf, pw.Length, salt.Length);
        return Convert.ToHexString(SHA256.HashData(buf));
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test --filter "FullyQualifiedName~PasswordHasherTests"
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add PasswordHasher (SHA-256 + salt, constant-time compare)"
```

---

## Task 4: KeyCombinationParser（Core，TDD）

**Files:**
- Create: `src/KioskWin/Core/KeyCombinationParser.cs`
- Create: `tests/KioskWin.Tests/KeyCombinationParserTests.cs`

**Interfaces:**
- Produces: `KioskWin.Core.KeyCombinationParser`
  - `static System.Windows.Forms.Keys Parse(string text)` — parses `"Ctrl+Shift+Alt+Q"` into a bitwise-OR `Keys` value
- Consumes: `System.Windows.Forms.Keys` (the main project already uses WinForms).

- [ ] **Step 1: Write a failing test**

Create `tests/KioskWin.Tests/KeyCombinationParserTests.cs`:

```csharp
using System.Windows.Forms;
using KioskWin.Core;
using Xunit;

namespace KioskWin.Tests;

public class KeyCombinationParserTests
{
    [Fact]
    public void Parses_full_modifier_combo_with_letter()
    {
        var keys = KeyCombinationParser.Parse("Ctrl+Shift+Alt+Q");
        Assert.Equal(Keys.Control | Keys.Shift | Keys.Alt | Keys.Q, keys);
    }

    [Fact]
    public void Parses_ctrl_only_with_digit()
    {
        var keys = KeyCombinationParser.Parse("Ctrl+1");
        Assert.Equal(Keys.Control | Keys.D1, keys);
    }

    [Fact]
    public void Parses_single_letter()
    {
        Assert.Equal(Keys.Q, KeyCombinationParser.Parse("Q"));
    }

    [Fact]
    public void Empty_returns_none()
    {
        Assert.Equal(Keys.None, KeyCombinationParser.Parse(""));
        Assert.Equal(Keys.None, KeyCombinationParser.Parse("   "));
    }

    [Fact]
    public void Unknown_token_is_ignored()
    {
        var keys = KeyCombinationParser.Parse("Ctrl+Banana+Q");
        Assert.Equal(Keys.Control | Keys.Q, keys);
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~KeyCombinationParserTests"
```

Expected: failure because `KeyCombinationParser` is undefined.

- [ ] **Step 3: Implement KeyCombinationParser**

Create `src/KioskWin/Core/KeyCombinationParser.cs`:

```csharp
using System.Windows.Forms;

namespace KioskWin.Core;

public static class KeyCombinationParser
{
    public static Keys Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Keys.None;

        Keys result = Keys.None;
        foreach (var raw in text.Split('+'))
        {
            var t = raw.Trim();
            if (t.Length == 0) continue;
            result |= MapToken(t);
        }
        return result;
    }

    private static Keys MapToken(string t) => t.ToLowerInvariant() switch
    {
        "ctrl" or "control" => Keys.Control,
        "shift" => Keys.Shift,
        "alt" or "menu" => Keys.Alt,
        var d when d.Length == 1 && char.IsLetterOrDigit(d[0]) => (Keys)char.ToUpper(d[0]),
        _ => Enum.TryParse(t, ignoreCase: true, out Keys k) ? k : Keys.None,
    };
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test --filter "FullyQualifiedName~KeyCombinationParserTests"
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add KeyCombinationParser (string -> WinForms Keys)"
```

---

## Task 5: KioskConfig（Core，TDD）

**Files:**
- Create: `src/KioskWin/Core/KioskConfig.cs`
- Create: `tests/KioskWin.Tests/KioskConfigTests.cs`

**Interfaces:**
- Consumes: `Microsoft.Extensions.Configuration.IConfiguration`
- Produces: `KioskWin.Core.KioskConfig`
  - Writable properties: `Url`, `AdminKeyCombination`, `AdminPasswordHash`, `PasswordSalt`, `RetryIntervalSeconds`, `TopMost`, `ShowInTaskbar`, `UserDataFolder`
  - Computed properties: `bool IsUrlValid`, `TimeSpan RetryInterval`
  - `static KioskConfig Load(IConfiguration configuration)` — bind settings and apply defaults (testable)
  - `static KioskConfig LoadFromFile(string jsonFilePath)` — read a JSON file (used by the application)

- [ ] **Step 1: Write a failing test**

Create `tests/KioskWin.Tests/KioskConfigTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using KioskWin.Core;
using Xunit;

namespace KioskWin.Tests;

public class KioskConfigTests
{
    private static IConfiguration ConfigFromMemory(params (string Key, string Value)[] pairs)
    {
        var dict = pairs.ToDictionary(p => p.Key, p => (string?)p.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Loads_all_fields()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("Url", "https://example.com/"),
            ("AdminKeyCombination", "Ctrl+Alt+P"),
            ("AdminPasswordHash", "ABCD"),
            ("PasswordSalt", "1234"),
            ("RetryIntervalSeconds", "30"),
            ("TopMost", "false"),
            ("ShowInTaskbar", "true"),
            ("UserDataFolder", "Data")
        ));

        Assert.Equal("https://example.com/", cfg.Url);
        Assert.Equal("Ctrl+Alt+P", cfg.AdminKeyCombination);
        Assert.Equal("ABCD", cfg.AdminPasswordHash);
        Assert.Equal("1234", cfg.PasswordSalt);
        Assert.Equal(30, cfg.RetryIntervalSeconds);
        Assert.False(cfg.TopMost);
        Assert.True(cfg.ShowInTaskbar);
        Assert.Equal("Data", cfg.UserDataFolder);
    }

    [Fact]
    public void IsUrlValid_true_for_https()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(("Url", "https://position.lmding.cn/x")));
        Assert.True(cfg.IsUrlValid);
    }

    [Fact]
    public void IsUrlValid_false_for_relative()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(("Url", "not a url")));
        Assert.False(cfg.IsUrlValid);
        // Preserve an invalid URL as-is.
        Assert.Equal("not a url", cfg.Url);
    }

    [Fact]
    public void Defaults_when_fields_missing()
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(("Url", "https://x")));
        Assert.Equal("Ctrl+Shift+Alt+Q", cfg.AdminKeyCombination);
        Assert.Equal("WebView2Data", cfg.UserDataFolder);
        Assert.Equal(10, cfg.RetryIntervalSeconds);
        Assert.True(cfg.TopMost);
        Assert.False(cfg.ShowInTaskbar);
    }

    [Theory]
    [InlineData(0, 10)]      // Invalid -> default to 10
    [InlineData(-5, 10)]     // Invalid -> default to 10
    [InlineData(3, 3)]
    [InlineData(99999, 600)] // Upper limit
    public void RetryInterval_clamped(int input, int expectedSeconds)
    {
        var cfg = KioskConfig.Load(ConfigFromMemory(
            ("Url", "https://x"),
            ("RetryIntervalSeconds", input.ToString())));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), cfg.RetryInterval);
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~KioskConfigTests"
```

Expected: failure because `KioskConfig` is undefined.

- [ ] **Step 3: Implement KioskConfig**

Create `src/KioskWin/Core/KioskConfig.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace KioskWin.Core;

public sealed class KioskConfig
{
    public string Url { get; set; } = "";
    public string AdminKeyCombination { get; set; } = "Ctrl+Shift+Alt+Q";
    public string AdminPasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public int RetryIntervalSeconds { get; set; } = 10;
    public bool TopMost { get; set; } = true;
    public bool ShowInTaskbar { get; set; } = false;
    public string UserDataFolder { get; set; } = "WebView2Data";

    public bool IsUrlValid =>
        Uri.TryCreate(Url, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public TimeSpan RetryInterval => TimeSpan.FromSeconds(Math.Clamp(RetryIntervalSeconds, 1, 600));

    public static KioskConfig Load(IConfiguration configuration)
    {
        var cfg = new KioskConfig();
        configuration.Bind(cfg);

        // Bind overwrites the defaults above with JSON values; normalize invalid or empty values here.
        if (string.IsNullOrWhiteSpace(cfg.AdminKeyCombination))
            cfg.AdminKeyCombination = "Ctrl+Shift+Alt+Q";
        if (string.IsNullOrWhiteSpace(cfg.UserDataFolder))
            cfg.UserDataFolder = "WebView2Data";
        if (cfg.RetryIntervalSeconds <= 0)
            cfg.RetryIntervalSeconds = 10;
        cfg.RetryIntervalSeconds = Math.Clamp(cfg.RetryIntervalSeconds, 1, 600);

        return cfg;
    }

    public static KioskConfig LoadFromFile(string jsonFilePath)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(jsonFilePath, optional: true, reloadOnChange: false);
        return Load(builder.Build());
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test --filter "FullyQualifiedName~KioskConfigTests"
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add KioskConfig with validation and safe defaults"
```

---

## Task 6: RetryController + IRetryScheduler（Core，TDD）

**Files:**
- Create: `src/KioskWin/Core/RetryController.cs`
- Create: `src/KioskWin/Core/FormsRetryScheduler.cs`
- Create: `tests/KioskWin.Tests/RetryControllerTests.cs`

**Interfaces:**
- Produces:
  - `interface IRetryScheduler { bool IsRunning { get; } void Start(TimeSpan interval, Action onTick); void Stop(); }`
  - `enum RetryState { Idle, Retrying }`
  - `sealed class RetryController`：ctor `(TimeSpan interval, IRetryScheduler scheduler)`；`RetryState State { get; }`；`event Action? ShouldRetry`；`void ReportFailure()`；`void ReportSuccess()`
  - `sealed class FormsRetryScheduler : IRetryScheduler` (based on `System.Windows.Forms.Timer`, ticks on the UI thread)
- Behavior: `ReportFailure` transitions from Idle to Retrying and starts the scheduler; each tick invokes `ShouldRetry`; `ReportSuccess` stops the scheduler and returns to Idle; repeated `ReportFailure` calls do not restart it.

> Note: The specification narrative mentions three states (`Idle/Waiting/Retrying`), but `Waiting` has no independently testable behavior, so YAGNI merges it into a two-state `Idle/Retrying` model; Task 6 tests cover the state machine behavior.

- [ ] **Step 1: Write a failing test**

Create `tests/KioskWin.Tests/RetryControllerTests.cs`:

```csharp
using KioskWin.Core;
using Xunit;

namespace KioskWin.Tests;

public class RetryControllerTests
{
    private sealed class FakeScheduler : IRetryScheduler
    {
        public bool IsRunning { get; private set; }
        public TimeSpan LastInterval { get; private set; }
        private Action? _onTick;

        public void Start(TimeSpan interval, Action onTick)
        {
            if (IsRunning) return;
            IsRunning = true;
            LastInterval = interval;
            _onTick = onTick;
        }

        public void Stop()
        {
            IsRunning = false;
            _onTick = null;
        }

        public void FireTick() => _onTick?.Invoke();
    }

    [Fact]
    public void ReportFailure_starts_retrying()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(15), sched);

        ctrl.ReportFailure();

        Assert.Equal(RetryState.Retrying, ctrl.State);
        Assert.True(sched.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(15), sched.LastInterval);
    }

    [Fact]
    public void Tick_raises_ShouldRetry()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(1), sched);
        var fired = 0;
        ctrl.ShouldRetry += () => fired++;

        ctrl.ReportFailure();
        sched.FireTick();
        sched.FireTick();

        Assert.Equal(2, fired);
    }

    [Fact]
    public void ReportSuccess_stops_and_returns_to_idle()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(1), sched);

        ctrl.ReportFailure();
        ctrl.ReportSuccess();

        Assert.Equal(RetryState.Idle, ctrl.State);
        Assert.False(sched.IsRunning);
    }

    [Fact]
    public void Double_failure_does_not_restart_or_change_interval()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(7), sched);

        ctrl.ReportFailure();
        ctrl.ReportFailure(); // A subsequent failure should be ignored.

        Assert.Equal(RetryState.Retrying, ctrl.State);
        Assert.Equal(TimeSpan.FromSeconds(7), sched.LastInterval);
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~RetryControllerTests"
```

Expected: failure because `RetryController` / `IRetryScheduler` are undefined.

- [ ] **Step 3: Implement RetryController + IRetryScheduler**

Create `src/KioskWin/Core/RetryController.cs`:

```csharp
namespace KioskWin.Core;

public interface IRetryScheduler
{
    bool IsRunning { get; }
    void Start(TimeSpan interval, Action onTick);
    void Stop();
}

public enum RetryState
{
    Idle,
    Retrying,
}

public sealed class RetryController
{
    private readonly TimeSpan _interval;
    private readonly IRetryScheduler _scheduler;

    public RetryController(TimeSpan interval, IRetryScheduler scheduler)
    {
        _interval = interval;
        _scheduler = scheduler;
    }

    public RetryState State { get; private set; } = RetryState.Idle;
    public event Action? ShouldRetry;

    public void ReportFailure()
    {
        if (State == RetryState.Retrying) return;
        State = RetryState.Retrying;
        _scheduler.Start(_interval, () => ShouldRetry?.Invoke());
    }

    public void ReportSuccess()
    {
        _scheduler.Stop();
        State = RetryState.Idle;
    }
}
```

- [ ] **Step 4: Implement the production FormsRetryScheduler**

Create `src/KioskWin/Core/FormsRetryScheduler.cs`:

```csharp
using System.Windows.Forms;

namespace KioskWin.Core;

public sealed class FormsRetryScheduler : IRetryScheduler
{
    private readonly Timer _timer = new() { Enabled = false };
    private Action? _onTick;

    public bool IsRunning => _timer.Enabled;

    public void Start(TimeSpan interval, Action onTick)
    {
        if (_timer.Enabled) return;
        _onTick = onTick;
        _timer.Interval = Math.Max((int)interval.TotalMilliseconds, 1);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _onTick = null;
    }

    private void OnTick(object? sender, EventArgs e) => _onTick?.Invoke();
}
```

- [ ] **Step 5: Run all tests to confirm they pass**

```bash
dotnet test
```

Expected: all Core tests pass (FileLogger / PasswordHasher / KeyCombinationParser / KioskConfig / RetryController).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): add RetryController state machine and FormsRetryScheduler"
```

---

## Task 7: RetryOverlay（Forms，UI）

**Files:**
- Create: `src/KioskWin/Forms/RetryOverlay.cs`

**Interfaces:**
- Produces: `KioskWin.Forms.RetryOverlay` (inherits `UserControl`, `Dock = Fill`)
  - `void ShowRetry()` / `void ShowConfigError()` / `void ShowRuntimeMissing()` / `void ShowGenericError(string? detail = null)`
- Consumes: `System.Windows.Forms`, `System.Drawing`.

> Note: Section 6 of the specification states that `RetryOverlay` is the **only** full-screen overlay component, with text changed through different methods. Construct this control in code (without `.Designer.cs`).

- [ ] **Step 1: Implement RetryOverlay**

Create `src/KioskWin/Forms/RetryOverlay.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace KioskWin.Forms;

public sealed class RetryOverlay : UserControl
{
    private readonly Label _label;

    public RetryOverlay()
    {
        BackColor = Color.FromArgb(30, 30, 30);
        Dock = DockStyle.Fill;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
        };

        _label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        table.Controls.Add(_label, 0, 0);
        Controls.Add(table);
    }

    private void SetMessage(string title, string detail)
        => _label.Text = string.IsNullOrEmpty(detail) ? title : $"{title}\n\n{detail}";

    public void ShowRetry() => SetMessage("Retrying…", "Network connection failed. Retrying automatically.");
    public void ShowConfigError() => SetMessage("Configuration error", "The URL is invalid. Contact your administrator.");
    public void ShowRuntimeMissing() => SetMessage("WebView2 runtime missing", "WebView2 runtime was not detected. Contact your administrator to install it.");
    public void ShowGenericError(string? detail = null) => SetMessage("Program error", detail ?? "An unknown error occurred. Check the log.");
}
```

- [ ] **Step 2: Build to confirm compilation succeeds**

```bash
dotnet build src/KioskWin/KioskWin.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(forms): add reusable RetryOverlay control"
```

---

## Task 8: AdminDialog（Forms，UI）

**Files:**
- Create: `src/KioskWin/Forms/AdminDialog.cs`

**Interfaces:**
- Consumes: `KioskWin.Core.KioskConfig`、`KioskWin.Core.PasswordHasher`
- Produces:
  - `enum KioskWin.Forms.AdminAction { None, Exit, Reload, DevTools, Unlock }`
  - `sealed class AdminDialog : Form`: ctor `(KioskConfig config, Action<string> logFailure)`; property `AdminAction Result`
- Behavior: enter a password → click "Verify" → `PasswordHasher.Verify` checks `config.AdminPasswordHash`/`config.PasswordSalt`; on success, enable four action buttons (Exit/Reload/DevTools/Unlock); on failure, show a message, clear the field, and log the failure.

- [ ] **Step 1: Implement AdminDialog**

Create `src/KioskWin/Forms/AdminDialog.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;
using KioskWin.Core;

namespace KioskWin.Forms;

public enum AdminAction
{
    None,
    Exit,
    Reload,
    DevTools,
    Unlock,
}

public sealed class AdminDialog : Form
{
    private readonly KioskConfig _config;
    private readonly Action<string> _logFailure;

    private readonly TextBox _passwordBox;
    private readonly Button _verifyButton;
    private readonly Button _exitButton;
    private readonly Button _reloadButton;
    private readonly Button _devToolsButton;
    private readonly Button _unlockButton;

    public AdminAction Result { get; private set; } = AdminAction.None;

    public AdminDialog(KioskConfig config, Action<string> logFailure)
    {
        _config = config;
        _logFailure = logFailure;

        Text = "KioskWin Administrator";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 280);

        var lbl = new Label
        {
            Text = "Admin password:",
            Location = new Point(20, 25),
            AutoSize = true,
        };

        _passwordBox = new TextBox
        {
            Location = new Point(20, 50),
            Size = new Size(320, 25),
            PasswordChar = '*',
        };
        _passwordBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { VerifyPassword(); e.SuppressKeyPress = true; }
        };

        _verifyButton = new Button { Text = "Verify", Location = new Point(250, 85), Size = new Size(90, 30) };
        _verifyButton.Click += (_, _) => VerifyPassword();

        _exitButton = MakeActionButton("Exit", 20, AdminAction.Exit);
        _reloadButton = MakeActionButton("Reload page", 110, AdminAction.Reload);
        _devToolsButton = MakeActionButton("Open DevTools", 200, AdminAction.DevTools);
        _unlockButton = MakeActionButton("Unlock", 20, AdminAction.Unlock);

        var unlockLabel = new Label
        {
            Text = "(Unlock: temporarily switch to a movable window for maintenance.)",
            Location = new Point(20, 195),
            Size = new Size(320, 60),
            ForeColor = Color.Gray,
        };

        Controls.AddRange(new Control[]
        {
            lbl, _passwordBox, _verifyButton,
            _exitButton, _reloadButton, _devToolsButton, _unlockButton, unlockLabel,
        });

        SetActionButtonsEnabled(false);
        AcceptButton = _verifyButton;
    }

    private Button MakeActionButton(string text, int x, AdminAction action)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, 150),
            Size = new Size(110, 35),
            Enabled = false,
        };
        btn.Click += (_, _) =>
        {
            Result = action;
            DialogResult = DialogResult.OK;
            Close();
        };
        return btn;
    }

    private void SetActionButtonsEnabled(bool enabled)
    {
        _exitButton.Enabled = enabled;
        _reloadButton.Enabled = enabled;
        _devToolsButton.Enabled = enabled;
        _unlockButton.Enabled = enabled;
    }

    private void VerifyPassword()
    {
        if (PasswordHasher.Verify(_passwordBox.Text, _config.AdminPasswordHash, _config.PasswordSalt))
        {
            SetActionButtonsEnabled(true);
            _passwordBox.Enabled = false;
            _verifyButton.Enabled = false;
            _ = MessageBox.Show(this, "Verification successful. Select an action.", "KioskWin",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _logFailure("admin auth failed");
            MessageBox.Show(this, "Incorrect password.", "KioskWin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _passwordBox.Clear();
        }
    }
}
```

- [ ] **Step 2: Build to confirm compilation succeeds**

```bash
dotnet build src/KioskWin/KioskWin.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(forms): add AdminDialog with password gate and tool actions"
```

---

## Task 9: MainForm (Forms integration)

**Files:**
- Create: `src/KioskWin/Forms/MainForm.cs`

**Interfaces:**
- Consumes: `KioskConfig`、`FileLogger`、`KeyCombinationParser`、`RetryController`、`FormsRetryScheduler`、`RetryOverlay`、`AdminDialog`/`AdminAction`、`Microsoft.Web.WebView2.WinForms.WebView2`、`CoreWebView2Environment`。
- Produces: `KioskWin.Forms.MainForm`, ctor `(KioskConfig config, FileLogger logger)`. `Program.cs` (Task 10) calls `Application.Run(new MainForm(config, logger))`.

> Covers the window-side requirements F1, F3, F4, F5, F7, and F8. This task has no unit tests (Win32/WebView2 behavior); use the manual QA checklist (Task 11).

- [ ] **Step 1: Implement MainForm**

Create `src/KioskWin/Forms/MainForm.cs`:

```csharp
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using KioskWin.Core;
using System.Drawing;
using System.Windows.Forms;
using KioskWin.Forms;

namespace KioskWin.Forms;

public sealed class MainForm : Form
{
    private KioskConfig _config;
    private readonly FileLogger _logger;
    private Keys _adminCombo;

    private readonly WebView2 _webView;
    private readonly RetryOverlay _overlay;
    private readonly RetryController _retry;
    private readonly FormsRetryScheduler _scheduler;

    private bool _authorizedClose;
    private bool _devToolsEnabled;

    public MainForm(KioskConfig config, FileLogger logger)
    {
        _config = config;
        _logger = logger;
        _adminCombo = KeyCombinationParser.Parse(config.AdminKeyCombination);

        // F3: borderless full-screen with no minimize or close buttons
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        // F5: prevent switching to the background
        TopMost = config.TopMost;
        ShowInTaskbar = config.ShowInTaskbar;
        KeyPreview = true;
        BackColor = Color.Black;

        _webView = new WebView2 { Dock = DockStyle.Fill };
        _overlay = new RetryOverlay { Visible = false };

        Controls.Add(_webView);
        Controls.Add(_overlay);
        _overlay.BringToFront();

        _scheduler = new FormsRetryScheduler();
        _retry = new RetryController(config.RetryInterval, _scheduler);
        _retry.ShouldRetry += OnRetry;

        Load += OnLoad;
        FormClosing += OnFormClosing;   // F4
        Deactivate += OnDeactivate;     // F5
    }

    // ---- WebView2 initialization and navigation ----

    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            var userDataFolder = Path.Combine(AppContext.BaseDirectory, _config.UserDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            ConfigureWebView();

            _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
            _webView.NavigationCompleted += OnNavigationCompleted;

            _logger.Log($"[startup] navigating to {_config.Url}");
            NavigateOrShowConfigError();
        }
        catch (Exception ex)
        {
            // For example, when the WebView2 runtime is not detected.
            _logger.Log($"[webview2 init] {ex}");
            _overlay.ShowRuntimeMissing();
            _overlay.Visible = true;
            _overlay.BringToFront();
        }
    }

    private void ConfigureWebView()
    {
        var s = _webView.CoreWebView2!.Settings;
        s.AreDevToolsEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsBuiltInErrorPageEnabled = false;
        s.IsGeneralAutofillEnabled = false;
    }

    private void NavigateOrShowConfigError()
    {
        if (_webView.CoreWebView2 == null) return;

        if (_config.IsUrlValid)
        {
            _webView.CoreWebView2.Navigate(_config.Url);
        }
        else
        {
            _logger.Log($"[config] invalid Url: '{_config.Url}'");
            _overlay.ShowConfigError();
            _overlay.Visible = true;
            _overlay.BringToFront();
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _retry.ReportSuccess();
            _overlay.Visible = false;
            _logger.Log("[navigation] success");
        }
        else
        {
            _logger.Log("[navigation] failed -> retry");
            _overlay.ShowRetry();
            _overlay.Visible = true;
            _overlay.BringToFront();
            _retry.ReportFailure();
        }
    }

    private void OnRetry()
    {
        if (IsDisposed) return;
        if (_config.IsUrlValid && _webView.CoreWebView2 != null)
        {
            BeginInvoke(() => _webView.CoreWebView2.Navigate(_config.Url));
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _logger.Log($"[webview2] process failed: {e.ProcessFailedKind}");
        _overlay.ShowGenericError("The web page rendering process crashed. Retrying…");
        _overlay.Visible = true;
        _overlay.BringToFront();
        _retry.ReportFailure();
        _ = ReinitAsync();
    }

    private async Task ReinitAsync()
    {
        try
        {
            await Task.Delay(2000);
            if (_webView.CoreWebView2 == null)
                await _webView.EnsureCoreWebView2Async();
            NavigateOrShowConfigError();
        }
        catch (Exception ex)
        {
            _logger.Log($"[reinit] {ex}");
        }
    }

    // ---- Lock behavior ----

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // F4: intercept user-initiated closing (Alt+F4 / close button) unless authorized by an administrator.
        if (!_authorizedClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _logger.Log("[close] intercepted user close");
        }
    }

    private void OnDeactivate(object? sender, EventArgs e)
    {
        // F5: reclaim the foreground after losing focus.
        if (TopMost)
            BeginInvoke(() => Activate());
    }

    // ---- Administrator escape path (F8) ----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_adminCombo != Keys.None && keyData == _adminCombo)
        {
            ShowAdminDialog();
            return true; // Consume the keystroke.
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowAdminDialog()
    {
        using var dlg = new AdminDialog(_config, message => _logger.Log(message));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        switch (dlg.Result)
        {
            case AdminAction.Exit:
                _logger.Log("[admin] exit requested");
                _authorizedClose = true;
                Application.Exit();
                break;
            case AdminAction.Reload:
                _logger.Log("[admin] reload requested");
                ReloadConfigAndNavigate();
                break;
            case AdminAction.DevTools:
                _logger.Log("[admin] toggle devtools");
                ToggleDevTools();
                break;
            case AdminAction.Unlock:
                _logger.Log("[admin] unlock mode");
                EnterUnlockedMode();
                break;
        }
    }

    private void ReloadConfigAndNavigate()
    {
        _config = KioskConfig.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        _adminCombo = KeyCombinationParser.Parse(_config.AdminKeyCombination);
        _logger.Log($"[reload] url={_config.Url}");
        NavigateOrShowConfigError();
    }

    private void ToggleDevTools()
    {
        if (_webView.CoreWebView2 == null) return;
        _devToolsEnabled = !_devToolsEnabled;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = _devToolsEnabled;
        if (_devToolsEnabled)
            _webView.CoreWebView2.OpenDevToolsWindow();
    }

    private void EnterUnlockedMode()
    {
        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Normal;
        Bounds = new Rectangle(50, 50, 1024, 768);
    }
}
```

- [ ] **Step 2: Build + run tests (ensure Core tests still pass)**

```bash
dotnet build KioskWin.sln
dotnet test
```

Expected: build has 0 errors; all Core tests still pass (MainForm has no unit tests).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(forms): add MainForm (fullscreen, lockdown, retry, admin entry)"
```

---

## Task 10: Final Program.cs entry point (Forms)

**Files:**
- Modify: `src/KioskWin/Program.cs` (replace the Task 1 placeholder)

**Interfaces:**
- Consumes: `KioskConfig.LoadFromFile`、`FileLogger`、`Forms.MainForm`。
- Produces: Complete application entry point, covering the F8 `--hash-password` CLI, F9 single-instance behavior, and N3 exception handling.

> Covers requirements F2 (configuration), F8 (hash-generation CLI), F9 (single instance), and N3 (no silent crashes).

- [ ] **Step 1: Replace Program.cs with the final version**

Replace the entire contents of `src/KioskWin/Program.cs` with:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using KioskWin.Core;
using KioskWin.Forms;

namespace KioskWin;

internal static class Program
{
    private const string MutexName = "Global\\KioskWin_SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        // F8: generate a password hash in CLI mode without starting the UI
        if (TryHandleHashPassword(args))
            return;

        ApplicationConfiguration.Initialize();

        // F9: single instance — bring the existing instance to the foreground and exit
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            BringExistingToFront();
            return;
        }

        // N3: handle unhandled exceptions globally to avoid silent crashes
        Application.ThreadException += (_, e) => LogException(e.Exception, "UI thread exception");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException(e.ExceptionObject as Exception, "AppDomain unhandled");

        try
        {
            var logger = new FileLogger();
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var config = KioskConfig.LoadFromFile(configPath);
            logger.Log($"[startup] url={config.Url} topMost={config.TopMost}");
            Application.Run(new MainForm(config, logger));
        }
        catch (Exception ex)
        {
            LogException(ex, "startup");
            MessageBox.Show(
                "The program failed to start. Check the log:\n" + ex.Message,
                "KioskWin",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static bool TryHandleHashPassword(string[] args)
    {
        if (args.Length == 2 &&
            string.Equals(args[0], "--hash-password", StringComparison.OrdinalIgnoreCase))
        {
            var result = PasswordHasher.Generate(args[1]);
            Console.WriteLine("Add these two lines to appsettings.json:");
            Console.WriteLine("  \"AdminPasswordHash\": \"" + result.Hash + "\",");
            Console.WriteLine("  \"PasswordSalt\": \"" + result.Salt + "\"");
            return true;
        }
        return false;
    }

    private static void BringExistingToFront()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(current.ProcessName))
            {
                if (p.Id == current.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                SetForegroundWindow(p.MainWindowHandle);
                ShowWindow(p.MainWindowHandle, SW_RESTORE);
            }
        }
        catch
        {
            // Best effort: failure to bring the window forward does not affect single-instance exit logic.
        }
    }

    private static void LogException(Exception? ex, string source)
    {
        try
        {
            new FileLogger().Log($"[{source}] {ex}");
        }
        catch
        {
            // Do not rethrow logging failures and cause a second exception.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
}
```

- [ ] **Step 2: Build**

```bash
dotnet build KioskWin.sln
```

Expected: 0 errors.

- [ ] **Step 3: Manually verify CLI mode**

```bash
dotnet run --project src/KioskWin/KioskWin.csproj -- --hash-password mysecret
```

Expected: print the two lines `AdminPasswordHash` / `PasswordSalt`; the application does not open a window and exits immediately.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: wire Program entry (hash-password CLI, single-instance, exception guards)"
```

---

## Task 11: Startup script + publish + manual QA

**Files:**
- Create: `src/KioskWin/install-shortcut.ps1`
- (Run) publish output + manual QA checklist

**Interfaces:**
- Produces: `install-shortcut.ps1` (creates a shortcut in the user's Startup folder); publish command; completed manual QA.

> Covers requirements F6 (startup on boot) and N1 (self-contained single file). This is a deployment and acceptance task.

- [ ] **Step 1: Write install-shortcut.ps1**

Create `src/KioskWin/install-shortcut.ps1`:

```powershell
# Create a KioskWin startup shortcut in the current user's Startup folder.
# Usage: Run powershell -ExecutionPolicy Bypass -File .\install-shortcut.ps1
# from the publish output directory containing KioskWin.exe.
$ErrorActionPreference = 'Stop'

$exe = Join-Path $PSScriptRoot 'KioskWin.exe'
if (-not (Test-Path $exe)) {
    throw "Could not find $exe. Run this script from the publish output directory."
}

$startup = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startup 'KioskWin.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.WindowStyle = 3   # Maximized
$shortcut.Description = 'KioskWin'
$shortcut.Save()

Write-Host "Created startup shortcut: $shortcutPath"
```

> Note: After publishing, this script should be next to `KioskWin.exe`. `dotnet publish` does not copy `.ps1` files to the output by default. The next step adds a publish setting to copy it.

- [ ] **Step 2: Configure the csproj to copy the script to publish output**

Insert this before `</Project>` in `src/KioskWin/KioskWin.csproj`:

```xml
  <ItemGroup>
    <None Update="install-shortcut.ps1">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 3: Publish as a self-contained single file**

```bash
dotnet publish src/KioskWin/KioskWin.csproj `
  -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

On Linux/macOS shells, remove the backtick line continuations and write the `dotnet publish ... -o publish` command on one line.

Expected: `publish/` contains `KioskWin.exe` (single file) + `appsettings.json` + `install-shortcut.ps1`.

> **Verify WebView2 single-file compatibility**: Copy `publish\` to a clean Win10/11 machine (with Edge/WebView2 Runtime installed), double-click `KioskWin.exe`, and confirm WebView2 initializes successfully (the page or retry overlay appears instead of a crash). If WebView2 fails to initialize in single-file mode (for example, due to `WebView2Loader.dll` extraction), republish without `-p:PublishSingleFile=true` as a fallback (still `--self-contained`, but deployed as a multi-file directory), and keep `install-shortcut.ps1` targeting `KioskWin.exe`.

- [ ] **Step 4: Configure the administrator password (important; otherwise maintenance exit is unavailable)**

Run from the publish directory:

```bash
./KioskWin.exe --hash-password your-password
```

Add the two output lines `AdminPasswordHash` / `PasswordSalt` to `appsettings.json` and save it.

- [ ] **Step 5: Complete the manual QA checklist on a physical industrial PC or Win10/11 virtual machine**

Double-click `KioskWin.exe` in `publish\` (or run `install-shortcut.ps1` and reboot), then verify each item:

- [ ] Full-screen over the primary display, with no title bar or minimize/close buttons
- [ ] Alt+F4 has no effect; the window does not close
- [ ] No taskbar entry
- [ ] Window stays on top; after Alt+Tab, the window remains visible in the foreground
- [ ] Disconnect the network → retry overlay appears; restore the network → page returns automatically
- [ ] Press Ctrl+Shift+Alt+Q to open the password dialog
- [ ] Incorrect password is rejected, reported, and cleared; correct password shows four action buttons
- [ ] Click "Exit" to close normally (Alt+F4 remains intercepted as expected)
- [ ] Click "Reload page" to reload `appsettings.json` (a changed URL takes effect)
- [ ] Click "Open DevTools" to open the DevTools window
- [ ] Click "Unlock" to switch the window to a movable normal window
- [ ] Application starts on boot (verify by running `install-shortcut.ps1` and rebooting)
- [ ] Double-clicking the exe while the app is running activates the existing window only (no second instance)

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add install-shortcut.ps1 and self-contained publish config"
```

---

## Completion criteria

- [ ] All tasks are committed; `dotnet test` passes (Core unit tests).
- [ ] `dotnet publish` output runs independently (no preinstalled .NET required).
- [ ] All manual QA checklist items are checked.
- [ ] `appsettings.json` contains a real, non-empty password hash.
