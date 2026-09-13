# YouTube Audio Downloader Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the broken Codex-generated WinForms app into a self-maintaining YouTube → MP3 clip tool that works out of the box on Windows 10/11 and is released from GitHub.

**Architecture:** One WinForms app (`src/YtAudioDownloader`) with small, pure, unit-tested helpers (time parsing, URL timestamp, file naming, ffmpeg/yt-dlp command building and output parsing), a thin process/tool layer (`ProcessRunner`, `YtDlpClient`, `FfmpegClient`), and a `ClipService` that orchestrates download → retry-after-update → cut/convert. The window only collects input and shows progress/errors. A PowerShell build script fetches yt-dlp (nightly), deno and ffmpeg and zips a self-contained single-file publish; GitHub Actions runs it on `v*` tags and publishes a Release.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), WinForms, xUnit, PowerShell (5.1 and 7 compatible), GitHub Actions, yt-dlp nightly, deno v2.9.6, ffmpeg 8.1 essentials (gyan.dev).

**Spec:** `docs/superpowers/specs/2026-09-13-yt-audio-rework-design.md`

## Global Constraints

- Target framework `net10.0-windows`, WinForms, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`. .NET 10 SDK is installed (`10.0.301`).
- Namespace `YtAudioDownloader` for app code (file-scoped `namespace YtAudioDownloader;` in new files); tests in `YtAudioDownloader.Tests`. New types are `internal` (tests see them through `InternalsVisibleTo`); only `MainForm` is `public`.
- Every number passed to ffmpeg/yt-dlp or written into a file name is formatted with `CultureInfo.InvariantCulture` (the friend's Windows is Russian and uses a decimal comma).
- External processes are started only through `ProcessRunner.RunAsync` using `ProcessStartInfo.ArgumentList` (never a concatenated argument string), with UTF-8 stdout/stderr and env `PYTHONIOENCODING=utf-8`.
- Code outside the UI (`MainForm*`) uses `.ConfigureAwait(false)` on every await.
- Paths: data folder `%LOCALAPPDATA%\YtAudioDownloader` (override with env `YTAUDIO_DATA_DIR`); bundled tools in `<app folder>\tools\` (`yt-dlp.exe`, `ffmpeg.exe`, `deno.exe`); logs in `<data folder>\logs\`; default output `%USERPROFILE%\Music\YtAudioDownloader`. UI language override env `YTAUDIO_LANG=ru|en`.
- yt-dlp update command: `--update-to nightly`. Pinned tool versions: deno `v2.9.6`, ffmpeg `8.1` essentials build from `GyanD/codexffmpeg`.
- Output: MP3 via `libmp3lame -q:a 2`, `-ar 44100`; normalize filter `loudnorm=I=-14:TP=-1.5:LRA=11`; default fade 3 s (range 0.5–10); default clip length 30 s.
- Release asset name `YtAudioDownloader.zip`, containing a top-level folder `YtAudioDownloader\`.
- Tests: xUnit; the whole suite runs with `dotnet test YtAudioDownloader.sln`.
- Git: work on branch `rework` (already checked out; repo-local identity already configured — do not change git config). Never push, never touch `git stash`. Every commit message ends with a blank line and `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- Comments: sparse; one short line where the "why" is not obvious. No XML doc comments.

---

### Task 1: Repo restructure, .NET 10, test project, CI test workflow

**Files:**
- Move: `MainForm.cs`, `MainForm.Designer.cs`, `Program.cs`, `YtAudioDownloader.csproj` → `src/YtAudioDownloader/`
- Modify: `src/YtAudioDownloader/YtAudioDownloader.csproj` (full replacement below)
- Create: `YtAudioDownloader.sln`, `tests/YtAudioDownloader.Tests/YtAudioDownloader.Tests.csproj`, `tests/YtAudioDownloader.Tests/ProjectReferenceTests.cs`, `.github/workflows/build.yml`
- Delete: `ffmpeg.exe`, `yt-dlp.exe`, `installer.iss`, `build.bat`, `.github/workflows/dotnet.yml`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing.
- Produces: solution `YtAudioDownloader.sln` with projects `src/YtAudioDownloader/YtAudioDownloader.csproj` and `tests/YtAudioDownloader.Tests/YtAudioDownloader.Tests.csproj`; app internals visible to `YtAudioDownloader.Tests`.

- [ ] **Step 1: Move the app into `src/`**

```powershell
git mv MainForm.cs src/YtAudioDownloader/MainForm.cs
git mv MainForm.Designer.cs src/YtAudioDownloader/MainForm.Designer.cs
git mv Program.cs src/YtAudioDownloader/Program.cs
git mv YtAudioDownloader.csproj src/YtAudioDownloader/YtAudioDownloader.csproj
```

(`git mv` needs the target folder: create `src/YtAudioDownloader` first with `New-Item -ItemType Directory -Force src/YtAudioDownloader`.)

- [ ] **Step 2: Replace `src/YtAudioDownloader/YtAudioDownloader.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Product>YouTube Audio Downloader</Product>
    <Version>0.0.0-dev</Version>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="YtAudioDownloader.Tests" />
  </ItemGroup>
</Project>
```

(The old `<None Include="yt-dlp.exe">` / `ffmpeg.exe` copy items are intentionally gone; tools are fetched by the build script in Task 7.)

- [ ] **Step 3: Remove the committed binaries and obsolete build files**

```powershell
git rm ffmpeg.exe yt-dlp.exe installer.iss build.bat .github/workflows/dotnet.yml
```

- [ ] **Step 4: Create the test project**

```powershell
dotnet new xunit -o tests/YtAudioDownloader.Tests -n YtAudioDownloader.Tests
Remove-Item tests/YtAudioDownloader.Tests/UnitTest1.cs
```

Then edit `tests/YtAudioDownloader.Tests/YtAudioDownloader.Tests.csproj`: keep every `PackageReference` and `<Using Include="Xunit" />` the template generated, but make the `PropertyGroup` and add the project reference so it reads:

```xml
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\YtAudioDownloader\YtAudioDownloader.csproj" />
  </ItemGroup>
```

- [ ] **Step 5: Write the reference smoke test**

`tests/YtAudioDownloader.Tests/ProjectReferenceTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class ProjectReferenceTests
{
    [Fact]
    public void Tests_reference_the_app_assembly()
    {
        Assert.Equal("YtAudioDownloader", typeof(MainForm).Assembly.GetName().Name);
    }
}
```

- [ ] **Step 6: Create the solution**

```powershell
dotnet new sln -n YtAudioDownloader --format sln
dotnet sln YtAudioDownloader.sln add src/YtAudioDownloader/YtAudioDownloader.csproj tests/YtAudioDownloader.Tests/YtAudioDownloader.Tests.csproj
```

- [ ] **Step 7: Update `.gitignore`**

Append these lines (keep the existing ones):

```
.tools-cache/
.superpowers/
TestResults/
```

- [ ] **Step 8: Create `.github/workflows/build.yml`**

```yaml
name: Build

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - name: Test
        run: dotnet test YtAudioDownloader.sln -c Release
```

- [ ] **Step 9: Build and test**

Run: `dotnet test YtAudioDownloader.sln`
Expected: build succeeds (the old `MainForm` still compiles), `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 10: Commit**

```powershell
git add -A
git commit -m "Move app to src/, target .NET 10, add test project and CI test workflow" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

Check with `git status` that `docs/` is included only if it was already untracked in the working tree (it is: the spec and this plan live there — committing them is intended).

---

### Task 2: Input and naming helpers (TimeInput, YouTubeUrl, OutputNaming)

**Files:**
- Create: `src/YtAudioDownloader/TimeInput.cs`, `src/YtAudioDownloader/YouTubeUrl.cs`, `src/YtAudioDownloader/OutputNaming.cs`
- Test: `tests/YtAudioDownloader.Tests/TimeInputTests.cs`, `tests/YtAudioDownloader.Tests/YouTubeUrlTests.cs`, `tests/YtAudioDownloader.Tests/OutputNamingTests.cs`

**Interfaces:**
- Consumes: project layout from Task 1.
- Produces:
  - `internal static class TimeInput` — `bool TryParse(string? text, out TimeSpan value)`, `string FormatCompact(TimeSpan time)` (`"1m35.5s"`, `"1h02m03s"`), `string FormatDisplay(TimeSpan time)` (`"1:35.5"`, `"1:02:03"`).
  - `internal static partial class YouTubeUrl` — `TimeSpan? TryGetStartTime(string? url)`.
  - `internal static partial class OutputNaming` — `string SanitizeTitle(string? title)`, `string BuildFileName(string? title, TimeSpan start, TimeSpan end)`, `string GetAvailablePath(string folder, string fileName, Func<string, bool>? exists = null)`.

- [ ] **Step 1: Write the failing tests**

`tests/YtAudioDownloader.Tests/TimeInputTests.cs`:

```csharp
using System.Globalization;

namespace YtAudioDownloader.Tests;

public class TimeInputTests
{
    [Theory]
    [InlineData("95", 95.0)]
    [InlineData("95.5", 95.5)]
    [InlineData("0", 0.0)]
    [InlineData("1:35", 95.0)]
    [InlineData("01:35", 95.0)]
    [InlineData("1:5", 65.0)]
    [InlineData("1:35.5", 95.5)]
    [InlineData("1:35,5", 95.5)]
    [InlineData("0:01:35", 95.0)]
    [InlineData("1:02:03", 3723.0)]
    [InlineData("1:02:03.25", 3723.25)]
    [InlineData("75:00", 4500.0)]
    [InlineData("  1:35  ", 95.0)]
    public void TryParse_accepts_human_time_formats(string text, double expectedSeconds)
    {
        Assert.True(TimeInput.TryParse(text, out TimeSpan value));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("5s")]
    [InlineData("-5")]
    [InlineData("1:60")]
    [InlineData("1:61:00")]
    [InlineData("1:-5")]
    [InlineData("1::5")]
    [InlineData(":5")]
    [InlineData("1:")]
    [InlineData("1.5:30")]
    [InlineData("1:2:3:4")]
    [InlineData("100:00:00")]
    public void TryParse_rejects_invalid_input(string? text)
    {
        Assert.False(TimeInput.TryParse(text, out _));
    }

    [Theory]
    [InlineData(95.0, "1m35s")]
    [InlineData(5.0, "0m05s")]
    [InlineData(125.0, "2m05s")]
    [InlineData(95.5, "1m35.5s")]
    [InlineData(95.25, "1m35.25s")]
    [InlineData(3723.0, "1h02m03s")]
    [InlineData(0.0, "0m00s")]
    public void FormatCompact_writes_file_name_friendly_times(double seconds, string expected)
    {
        Assert.Equal(expected, TimeInput.FormatCompact(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(95.0, "1:35")]
    [InlineData(5.0, "0:05")]
    [InlineData(125.0, "2:05")]
    [InlineData(95.5, "1:35.5")]
    [InlineData(3723.0, "1:02:03")]
    public void FormatDisplay_writes_what_TryParse_reads(double seconds, string expected)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        string text = TimeInput.FormatDisplay(time);

        Assert.Equal(expected, text);
        Assert.True(TimeInput.TryParse(text, out TimeSpan roundTrip));
        Assert.Equal(time, roundTrip);
    }

    [Fact]
    public void Formatting_uses_a_dot_even_on_russian_windows()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        try
        {
            Assert.Equal("1:35.5", TimeInput.FormatDisplay(TimeSpan.FromSeconds(95.5)));
            Assert.Equal("1m35.5s", TimeInput.FormatCompact(TimeSpan.FromSeconds(95.5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
```

`tests/YtAudioDownloader.Tests/YouTubeUrlTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class YouTubeUrlTests
{
    [Theory]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?t=95", 95)]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?si=abc123&t=30", 30)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY&t=95s", 95)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY&t=1m35s", 95)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY&t=1h2m3s", 3723)]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY#t=1m5s", 65)]
    [InlineData("https://www.youtube.com/embed/Ex-dEn5KAsY?start=42", 42)]
    public void TryGetStartTime_reads_the_shared_timestamp(string url, int expectedSeconds)
    {
        Assert.Equal<TimeSpan?>(TimeSpan.FromSeconds(expectedSeconds), YouTubeUrl.TryGetStartTime(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://www.youtube.com/watch?v=Ex-dEn5KAsY")]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?t=")]
    [InlineData("https://youtu.be/Ex-dEn5KAsY?t=abc")]
    public void TryGetStartTime_returns_null_without_a_timestamp(string? url)
    {
        Assert.Null(YouTubeUrl.TryGetStartTime(url));
    }
}
```

`tests/YtAudioDownloader.Tests/OutputNamingTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class OutputNamingTests
{
    [Theory]
    [InlineData("Crypt of The Seetherdancer", "Crypt of The Seetherdancer")]
    [InlineData("AC/DC: Back In Black?", "AC DC Back In Black")]
    [InlineData("Песня — тест", "Песня — тест")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("  spaced   out  ", "spaced out")]
    [InlineData("<>:\"/\\|?*", "audio")]
    [InlineData("", "audio")]
    [InlineData(null, "audio")]
    public void SanitizeTitle_keeps_only_what_windows_allows(string? title, string expected)
    {
        Assert.Equal(expected, OutputNaming.SanitizeTitle(title));
    }

    [Fact]
    public void SanitizeTitle_limits_length_to_120()
    {
        Assert.Equal(new string('a', 120), OutputNaming.SanitizeTitle(new string('a', 200)));
    }

    [Fact]
    public void SanitizeTitle_does_not_cut_an_emoji_in_half()
    {
        string title = new string('a', 119) + "😀" + "tail";

        Assert.Equal(new string('a', 119), OutputNaming.SanitizeTitle(title));
    }

    [Fact]
    public void BuildFileName_appends_the_clip_range()
    {
        Assert.Equal(
            "Crypt of The Seetherdancer (1m35s-2m05s).mp3",
            OutputNaming.BuildFileName("Crypt of The Seetherdancer", TimeSpan.FromSeconds(95), TimeSpan.FromSeconds(125)));
    }

    [Fact]
    public void GetAvailablePath_returns_the_plain_name_when_free()
    {
        Assert.Equal(@"C:\music\a.mp3", OutputNaming.GetAvailablePath(@"C:\music", "a.mp3", _ => false));
    }

    [Fact]
    public void GetAvailablePath_never_overwrites_existing_files()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\music\a.mp3", @"C:\music\a (2).mp3" };

        Assert.Equal(@"C:\music\a (3).mp3", OutputNaming.GetAvailablePath(@"C:\music", "a.mp3", existing.Contains));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test YtAudioDownloader.sln`
Expected: build FAILS with `CS0103: The name 'TimeInput' does not exist` (and the same for `YouTubeUrl`, `OutputNaming`).

- [ ] **Step 3: Implement `src/YtAudioDownloader/TimeInput.cs`**

```csharp
using System.Globalization;

namespace YtAudioDownloader;

// Times typed by a person: "95", "1:35", "1:02:03", optionally with a fraction ("1:35.5" or "1:35,5").
internal static class TimeInput
{
    private const double MaxSeconds = 100 * 3600;

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Trim().Replace(',', '.').Split(':');
        if (parts.Length > 3)
            return false;

        if (!double.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds))
            return false;
        if (parts.Length > 1 && seconds >= 60)
            return false;

        double minutes = 0;
        if (parts.Length >= 2)
        {
            if (!TryParseWhole(parts[^2], out long wholeMinutes))
                return false;
            if (parts.Length == 3 && wholeMinutes >= 60)
                return false;
            minutes = wholeMinutes;
        }

        double hours = 0;
        if (parts.Length == 3)
        {
            if (!TryParseWhole(parts[0], out long wholeHours))
                return false;
            hours = wholeHours;
        }

        double total = hours * 3600 + minutes * 60 + seconds;
        if (total >= MaxSeconds)
            return false;

        value = TimeSpan.FromMilliseconds(Math.Round(total * 1000));
        return true;
    }

    // 95.5 s -> "1m35.5s", 3723 s -> "1h02m03s"; used in output file names.
    public static string FormatCompact(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        return hours > 0
            ? $"{hours}h{time.Minutes:00}m{SecondsPart(time)}s"
            : $"{time.Minutes}m{SecondsPart(time)}s";
    }

    // 95.5 s -> "1:35.5", 3723 s -> "1:02:03"; the format TryParse reads back.
    public static string FormatDisplay(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        return hours > 0
            ? $"{hours}:{time.Minutes:00}:{SecondsPart(time)}"
            : $"{time.Minutes}:{SecondsPart(time)}";
    }

    private static string SecondsPart(TimeSpan time)
    {
        string seconds = time.Seconds.ToString("00", CultureInfo.InvariantCulture);
        return time.Milliseconds == 0
            ? seconds
            : seconds + "." + time.Milliseconds.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
    }

    private static bool TryParseWhole(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
```

- [ ] **Step 4: Implement `src/YtAudioDownloader/YouTubeUrl.cs`**

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace YtAudioDownloader;

internal static partial class YouTubeUrl
{
    // Reads the start time YouTube puts into shared links ("Share → Start at"): t=95, t=95s, t=1m35s, t=1h2m3s, start=95.
    public static TimeSpan? TryGetStartTime(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri))
            return null;

        string parameters = uri.Query.TrimStart('?') + "&" + uri.Fragment.TrimStart('#');
        foreach (string pair in parameters.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            if (equals <= 0 || pair[..equals] is not ("t" or "start"))
                continue;
            if (ParseTime(Uri.UnescapeDataString(pair[(equals + 1)..])) is TimeSpan time)
                return time;
        }
        return null;
    }

    private static TimeSpan? ParseTime(string value)
    {
        Match match = TimeRegex().Match(value);
        if (value.Length == 0 || !match.Success)
            return null;

        int Part(int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(Part(1), Part(2), Part(3));
    }

    [GeneratedRegex("^(?:([0-9]{1,4})h)?(?:([0-9]{1,6})m)?(?:([0-9]{1,7})s?)?$")]
    private static partial Regex TimeRegex();
}
```

- [ ] **Step 5: Implement `src/YtAudioDownloader/OutputNaming.cs`**

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace YtAudioDownloader;

internal static partial class OutputNaming
{
    private const int MaxTitleLength = 120;
    private const string FallbackTitle = "audio";

    // Turns a video title into something Windows accepts as (part of) a file name.
    public static string SanitizeTitle(string? title)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder();
        foreach (char c in title ?? string.Empty)
            builder.Append(char.IsControl(c) || Array.IndexOf(invalid, c) >= 0 ? ' ' : c);

        string cleaned = Whitespace().Replace(builder.ToString(), " ").Trim();
        if (cleaned.Length > MaxTitleLength)
        {
            int cut = char.IsHighSurrogate(cleaned[MaxTitleLength - 1]) ? MaxTitleLength - 1 : MaxTitleLength;
            cleaned = cleaned[..cut].TrimEnd();
        }
        return cleaned.Length == 0 ? FallbackTitle : cleaned;
    }

    public static string BuildFileName(string? title, TimeSpan start, TimeSpan end) =>
        $"{SanitizeTitle(title)} ({TimeInput.FormatCompact(start)}-{TimeInput.FormatCompact(end)}).mp3";

    // Adds " (2)", " (3)", ... so an existing file is never overwritten.
    public static string GetAvailablePath(string folder, string fileName, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = Path.Combine(folder, fileName);
        for (int n = 2; exists(candidate); n++)
            candidate = Path.Combine(folder, $"{stem} ({n}){extension}");
        return candidate;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test YtAudioDownloader.sln`
Expected: all tests PASS, 0 failed.

- [ ] **Step 7: Commit**

```powershell
git add src/YtAudioDownloader/TimeInput.cs src/YtAudioDownloader/YouTubeUrl.cs src/YtAudioDownloader/OutputNaming.cs tests/YtAudioDownloader.Tests/TimeInputTests.cs tests/YtAudioDownloader.Tests/YouTubeUrlTests.cs tests/YtAudioDownloader.Tests/OutputNamingTests.cs
git commit -m "Add time parsing, URL timestamp and output naming helpers" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: ffmpeg and yt-dlp command building and output parsing

**Files:**
- Create: `src/YtAudioDownloader/FfmpegCommand.cs`, `src/YtAudioDownloader/YtDlpCommand.cs`
- Test: `tests/YtAudioDownloader.Tests/FfmpegCommandTests.cs`, `tests/YtAudioDownloader.Tests/YtDlpCommandTests.cs`

**Interfaces:**
- Consumes: nothing from Task 2.
- Produces:
  - `internal static class FfmpegCommand` — `double EffectiveFadeSeconds(TimeSpan clipLength, double requestedSeconds)`, `string? BuildFilter(TimeSpan clipLength, bool fade, double fadeSeconds, bool normalize)`, `List<string> BuildArguments(string inputFile, string outputFile, TimeSpan start, TimeSpan clipLength, string? filter)`, `double? TryParseProgress(string line, TimeSpan clipLength)`.
  - `internal enum YtDlpFailure { Blocked, Unavailable, InvalidUrl, Network, Unknown }`
  - `internal sealed class YtDlpDownloadInfo { string? Title; TimeSpan? Duration; string? FilePath; }` (settable properties)
  - `internal static class YtDlpCommand` — `List<string> BuildDownloadArguments(string url, string tempDir, string? denoPath)`, `List<string> BuildUpdateArguments()`, `double? ReadLine(string line, YtDlpDownloadInfo info)`, `YtDlpFailure Classify(string stderr)`.

- [ ] **Step 1: Write the failing tests**

`tests/YtAudioDownloader.Tests/FfmpegCommandTests.cs`:

```csharp
using System.Globalization;

namespace YtAudioDownloader.Tests;

public class FfmpegCommandTests
{
    private const string Loudnorm = "loudnorm=I=-14:TP=-1.5:LRA=11";

    [Fact]
    public void BuildFilter_normalizes_then_fades()
    {
        Assert.Equal(
            Loudnorm + ",afade=t=in:st=0:d=3,afade=t=out:st=27:d=3",
            FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(30), fade: true, fadeSeconds: 3, normalize: true));
    }

    [Fact]
    public void BuildFilter_uses_a_dot_on_russian_windows()
    {
        string? filter = WithCulture("ru-RU", () =>
            FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(30.5), fade: true, fadeSeconds: 3, normalize: false));

        Assert.Equal("afade=t=in:st=0:d=3,afade=t=out:st=27.5:d=3", filter);
    }

    [Fact]
    public void BuildFilter_shortens_fades_for_short_clips()
    {
        Assert.Equal(
            "afade=t=in:st=0:d=2,afade=t=out:st=4:d=2",
            FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(6), fade: true, fadeSeconds: 3, normalize: false));
    }

    [Theory]
    [InlineData(false, 3.0, false, null)]
    [InlineData(true, 0.0, false, null)]
    [InlineData(false, 3.0, true, Loudnorm)]
    public void BuildFilter_omits_what_is_turned_off(bool fade, double fadeSeconds, bool normalize, string? expected)
    {
        Assert.Equal(expected, FfmpegCommand.BuildFilter(TimeSpan.FromSeconds(30), fade, fadeSeconds, normalize));
    }

    [Fact]
    public void EffectiveFadeSeconds_is_at_most_a_third_of_the_clip()
    {
        Assert.Equal(3, FfmpegCommand.EffectiveFadeSeconds(TimeSpan.FromSeconds(30), 3));
        Assert.Equal(1, FfmpegCommand.EffectiveFadeSeconds(TimeSpan.FromSeconds(3), 3));
    }

    [Fact]
    public void BuildArguments_cuts_converts_and_reports_progress()
    {
        List<string> args = WithCulture("ru-RU", () =>
            FfmpegCommand.BuildArguments("in.m4a", "out.mp3", TimeSpan.FromSeconds(95.5), TimeSpan.FromSeconds(30), "afade=t=in:st=0:d=3"));

        Assert.Equal(new[]
        {
            "-hide_banner", "-nostdin", "-y", "-ss", "95.5", "-i", "in.m4a", "-t", "30", "-vn",
            "-af", "afade=t=in:st=0:d=3",
            "-ar", "44100", "-c:a", "libmp3lame", "-q:a", "2", "-progress", "pipe:1", "-nostats", "out.mp3",
        }, args);
    }

    [Fact]
    public void BuildArguments_without_filter_has_no_af()
    {
        List<string> args = FfmpegCommand.BuildArguments("in.m4a", "out.mp3", TimeSpan.Zero, TimeSpan.FromSeconds(30), null);

        Assert.DoesNotContain("-af", args);
    }

    [Theory]
    [InlineData("out_time_us=15000000", 0.5)]
    [InlineData("out_time_us=45000000", 1.0)]
    [InlineData("out_time_us=0", 0.0)]
    public void TryParseProgress_reads_out_time(string line, double expected)
    {
        Assert.Equal(expected, FfmpegCommand.TryParseProgress(line, TimeSpan.FromSeconds(30)));
    }

    [Theory]
    [InlineData("out_time_us=N/A")]
    [InlineData("progress=end")]
    [InlineData("bitrate=128.0kbits/s")]
    public void TryParseProgress_ignores_other_lines(string line)
    {
        Assert.Null(FfmpegCommand.TryParseProgress(line, TimeSpan.FromSeconds(30)));
    }

    private static T WithCulture<T>(string name, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
```

`tests/YtAudioDownloader.Tests/YtDlpCommandTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class YtDlpCommandTests
{
    private const string Url = "https://www.youtube.com/watch?v=Ex-dEn5KAsY";

    [Fact]
    public void BuildDownloadArguments_passes_the_bundled_deno()
    {
        List<string> args = YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", @"C:\app\tools\deno.exe");

        int index = args.IndexOf("--js-runtimes");
        Assert.True(index >= 0);
        Assert.Equal(@"deno:C:\app\tools\deno.exe", args[index + 1]);
    }

    [Fact]
    public void BuildDownloadArguments_without_deno_has_no_js_runtimes()
    {
        Assert.DoesNotContain("--js-runtimes", YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", null));
    }

    [Fact]
    public void BuildDownloadArguments_ignores_user_config_and_ends_with_the_url()
    {
        List<string> args = YtDlpCommand.BuildDownloadArguments(Url, @"C:\tmp\job", null);

        Assert.Contains("--ignore-config", args);
        Assert.Contains("--no-update", args);
        Assert.Equal(@"C:\tmp\job\download.%(ext)s", args[args.IndexOf("-o") + 1]);
        Assert.Equal(new[] { "--", Url }, args.TakeLast(2));
    }

    [Fact]
    public void BuildUpdateArguments_switches_to_nightly()
    {
        Assert.Equal(new[] { "--ignore-config", "--update-to", "nightly" }, YtDlpCommand.BuildUpdateArguments());
    }

    [Fact]
    public void ReadLine_collects_title_duration_and_file()
    {
        var info = new YtDlpDownloadInfo();

        Assert.Null(YtDlpCommand.ReadLine("TITLE Crypt of The Seetherdancer", info));
        Assert.Null(YtDlpCommand.ReadLine("DURATION 205", info));
        Assert.Null(YtDlpCommand.ReadLine(@"FILE C:\tmp\job\download.m4a", info));

        Assert.Equal("Crypt of The Seetherdancer", info.Title);
        Assert.Equal(TimeSpan.FromSeconds(205), info.Duration);
        Assert.Equal(@"C:\tmp\job\download.m4a", info.FilePath);
    }

    [Theory]
    [InlineData("DURATION 205.5", 205.5)]
    [InlineData("DURATION NA", null)]
    public void ReadLine_parses_duration(string line, double? expectedSeconds)
    {
        var info = new YtDlpDownloadInfo();
        YtDlpCommand.ReadLine(line, info);

        Assert.Equal(expectedSeconds is double s ? TimeSpan.FromSeconds(s) : (TimeSpan?)null, info.Duration);
    }

    [Fact]
    public void ReadLine_returns_download_progress()
    {
        var info = new YtDlpDownloadInfo();

        Assert.Equal(1024.0 / 3315458, YtDlpCommand.ReadLine("PROGRESS 1024 3315458 NA", info)!.Value, 9);
        Assert.Equal(0.25, YtDlpCommand.ReadLine("PROGRESS 1000 NA 4000.0", info));
        Assert.Null(YtDlpCommand.ReadLine("PROGRESS 1000 NA NA", info));
        Assert.Null(YtDlpCommand.ReadLine("[download] Destination: x", info));
        Assert.Null(info.Title);
    }

    [Theory]
    [InlineData("ERROR: unable to download video data: HTTP Error 403: Forbidden", YtDlpFailure.Blocked)]
    [InlineData("ERROR: [youtube] x: Sign in to confirm you’re not a bot. Use --cookies-from-browser", YtDlpFailure.Blocked)]
    [InlineData("ERROR: Unable to download webpage: HTTP Error 429: Too Many Requests", YtDlpFailure.Blocked)]
    [InlineData("ERROR: [youtube] x: Private video. Sign in if you've been granted access to this video", YtDlpFailure.Unavailable)]
    [InlineData("ERROR: [youtube] x: Sign in to confirm your age. This video may be inappropriate for some users.", YtDlpFailure.Unavailable)]
    [InlineData("ERROR: [youtube] x: Video unavailable", YtDlpFailure.Unavailable)]
    [InlineData("ERROR: [generic] 'abc' is not a valid URL.", YtDlpFailure.InvalidUrl)]
    [InlineData("ERROR: Unsupported URL: https://example.com/", YtDlpFailure.InvalidUrl)]
    [InlineData("ERROR: [youtube] x: Unable to download webpage: <urlopen error [Errno 11001] getaddrinfo failed>", YtDlpFailure.Network)]
    [InlineData("ERROR: something nobody has seen before", YtDlpFailure.Unknown)]
    public void Classify_recognises_common_failures(string stderr, YtDlpFailure expected)
    {
        Assert.Equal(expected, YtDlpCommand.Classify(stderr));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test YtAudioDownloader.sln`
Expected: build FAILS with `CS0103`/`CS0246` for `FfmpegCommand`, `YtDlpCommand`, `YtDlpDownloadInfo`, `YtDlpFailure`.

- [ ] **Step 3: Implement `src/YtAudioDownloader/FfmpegCommand.cs`**

```csharp
using System.Globalization;

namespace YtAudioDownloader;

internal static class FfmpegCommand
{
    private const string NormalizeFilter = "loudnorm=I=-14:TP=-1.5:LRA=11";
    private const string ProgressPrefix = "out_time_us=";

    // Each fade takes at most a third of the clip, so the middle always plays at full volume.
    public static double EffectiveFadeSeconds(TimeSpan clipLength, double requestedSeconds) =>
        Math.Max(0, Math.Min(requestedSeconds, clipLength.TotalSeconds / 3));

    public static string? BuildFilter(TimeSpan clipLength, bool fade, double fadeSeconds, bool normalize)
    {
        var filters = new List<string>();
        if (normalize)
            filters.Add(NormalizeFilter);

        double fadeLength = fade ? EffectiveFadeSeconds(clipLength, fadeSeconds) : 0;
        if (fadeLength > 0)
        {
            filters.Add($"afade=t=in:st=0:d={Number(fadeLength)}");
            filters.Add($"afade=t=out:st={Number(clipLength.TotalSeconds - fadeLength)}:d={Number(fadeLength)}");
        }
        return filters.Count == 0 ? null : string.Join(",", filters);
    }

    public static List<string> BuildArguments(string inputFile, string outputFile, TimeSpan start, TimeSpan clipLength, string? filter)
    {
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-y",
            "-ss", Number(start.TotalSeconds), "-i", inputFile, "-t", Number(clipLength.TotalSeconds), "-vn",
        };
        if (filter != null)
        {
            args.Add("-af");
            args.Add(filter);
        }
        // loudnorm resamples to 192 kHz, so the output rate is set explicitly.
        args.AddRange(new[] { "-ar", "44100", "-c:a", "libmp3lame", "-q:a", "2", "-progress", "pipe:1", "-nostats", outputFile });
        return args;
    }

    // "-progress pipe:1" prints lines like "out_time_us=12345678".
    public static double? TryParseProgress(string line, TimeSpan clipLength)
    {
        if (clipLength <= TimeSpan.Zero || !line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
            return null;
        if (!long.TryParse(line.AsSpan(ProgressPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds))
            return null;
        return Math.Clamp(microseconds / 1_000_000.0 / clipLength.TotalSeconds, 0, 1);
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Implement `src/YtAudioDownloader/YtDlpCommand.cs`**

```csharp
using System.Globalization;

namespace YtAudioDownloader;

internal enum YtDlpFailure { Blocked, Unavailable, InvalidUrl, Network, Unknown }

// What yt-dlp reported about a download, collected line by line.
internal sealed class YtDlpDownloadInfo
{
    public string? Title { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? FilePath { get; set; }
}

internal static class YtDlpCommand
{
    private const string TitlePrefix = "TITLE ";
    private const string DurationPrefix = "DURATION ";
    private const string FilePrefix = "FILE ";
    private const string ProgressPrefix = "PROGRESS ";

    public static List<string> BuildDownloadArguments(string url, string tempDir, string? denoPath)
    {
        var args = new List<string>
        {
            "--ignore-config", "--no-update", "--no-playlist", "--no-part", "--no-mtime",
            "-f", "bestaudio[ext=m4a]/bestaudio",
        };
        if (denoPath != null)
        {
            args.Add("--js-runtimes");
            args.Add("deno:" + denoPath);
        }
        args.AddRange(new[]
        {
            "--no-simulate", "--progress", "--newline",
            "--progress-template", "download:" + ProgressPrefix + "%(progress.downloaded_bytes)s %(progress.total_bytes)s %(progress.total_bytes_estimate)s",
            "--print", "before_dl:" + TitlePrefix + "%(title)s",
            "--print", "before_dl:" + DurationPrefix + "%(duration)s",
            "--print", "after_move:" + FilePrefix + "%(filepath)s",
            "-o", Path.Combine(tempDir, "download.%(ext)s"),
            "--", url,
        });
        return args;
    }

    public static List<string> BuildUpdateArguments() => new() { "--ignore-config", "--update-to", "nightly" };

    // Records TITLE/DURATION/FILE lines into info; returns the download fraction (0..1) for PROGRESS lines, otherwise null.
    public static double? ReadLine(string line, YtDlpDownloadInfo info)
    {
        if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
            return ParseProgress(line[ProgressPrefix.Length..]);

        if (line.StartsWith(TitlePrefix, StringComparison.Ordinal))
            info.Title = line[TitlePrefix.Length..];
        else if (line.StartsWith(DurationPrefix, StringComparison.Ordinal) &&
                 TryParseNumber(line[DurationPrefix.Length..], out double seconds) && seconds > 0)
            info.Duration = TimeSpan.FromSeconds(seconds);
        else if (line.StartsWith(FilePrefix, StringComparison.Ordinal))
            info.FilePath = line[FilePrefix.Length..];
        return null;
    }

    public static YtDlpFailure Classify(string stderr)
    {
        if (ContainsAny(stderr, "HTTP Error 403", "HTTP Error 429", "not a bot", "Requested format is not available",
                "Signature extraction failed", "nsig extraction failed"))
            return YtDlpFailure.Blocked;
        if (ContainsAny(stderr, "is not a valid URL", "Unsupported URL", "Incomplete YouTube ID"))
            return YtDlpFailure.InvalidUrl;
        if (ContainsAny(stderr, "Private video", "Video unavailable", "This video is unavailable", "confirm your age",
                "members-only", "This live event will begin", "Premieres in"))
            return YtDlpFailure.Unavailable;
        if (ContainsAny(stderr, "getaddrinfo failed", "Failed to resolve", "timed out", "Connection refused",
                "Connection reset", "Network is unreachable", "No route to host", "Unable to download webpage"))
            return YtDlpFailure.Network;
        return YtDlpFailure.Unknown;
    }

    // "downloaded total estimate"; yt-dlp prints NA for unknown values.
    private static double? ParseProgress(string values)
    {
        string[] parts = values.Split(' ');
        if (parts.Length != 3 || !TryParseNumber(parts[0], out double downloaded))
            return null;
        if (!TryParseNumber(parts[1], out double total) && !TryParseNumber(parts[2], out total))
            return null;
        return total > 0 ? Math.Clamp(downloaded / total, 0, 1) : null;
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test YtAudioDownloader.sln`
Expected: all tests PASS, 0 failed.

- [ ] **Step 6: Commit**

```powershell
git add src/YtAudioDownloader/FfmpegCommand.cs src/YtAudioDownloader/YtDlpCommand.cs tests/YtAudioDownloader.Tests/FfmpegCommandTests.cs tests/YtAudioDownloader.Tests/YtDlpCommandTests.cs
git commit -m "Add ffmpeg/yt-dlp command building and output parsing" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Process and tool layer (AppPaths, Log, ProcessRunner, YtDlpClient, FfmpegClient)

**Files:**
- Create: `src/YtAudioDownloader/AppPaths.cs`, `src/YtAudioDownloader/Log.cs`, `src/YtAudioDownloader/ProcessRunner.cs`, `src/YtAudioDownloader/Errors.cs`, `src/YtAudioDownloader/ToolContracts.cs`, `src/YtAudioDownloader/YtDlpClient.cs`, `src/YtAudioDownloader/FfmpegClient.cs`
- Test: `tests/YtAudioDownloader.Tests/TestEnvironment.cs`, `tests/YtAudioDownloader.Tests/ProcessRunnerTests.cs`, `tests/YtAudioDownloader.Tests/ToolClientTests.cs`

**Interfaces:**
- Consumes: `YtDlpCommand`, `YtDlpDownloadInfo`, `YtDlpFailure`, `FfmpegCommand.TryParseProgress` (Task 3).
- Produces:
  - `internal static class AppPaths` — `string DataDir`, `string BundledToolsDir`, `string LogsDir`, `string TempRoot`, `string DefaultOutputDir`.
  - `internal static class Log` — `string CurrentFile`, `void Write(string message)`, `void DeleteOldFiles(int keep = 14)`.
  - `internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)`; `internal static class ProcessRunner` — `Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> arguments, Action<string>? onStdOutLine, CancellationToken cancellationToken)` (throws `OperationCanceledException` after killing the process tree when cancelled).
  - Exceptions: `ToolMissingException(string toolName, string expectedPath)` with `ToolName`; `YtDlpException(YtDlpFailure failure, string details)` with `Failure`, `Details`; `FfmpegException(string details)` with `Details`.
  - `internal sealed record DownloadedAudio(string FilePath, string? Title, TimeSpan? Duration)`; `internal sealed record UpdateOutcome(bool Succeeded, string? Version)`.
  - `internal interface IAudioDownloader` — `Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken)`, `Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken)`.
  - `internal interface IAudioConverter` — `Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken)`.
  - `internal sealed class YtDlpClient : IAudioDownloader` — ctor `(string exePath, string bundledExePath, string? denoPath)`, `static YtDlpClient CreateDefault()`, `Task<string> EnsureReadyAsync(CancellationToken)` (returns version).
  - `internal sealed class FfmpegClient : IAudioConverter` — ctor `(string exePath)`, `static FfmpegClient CreateDefault()`.

- [ ] **Step 1: Write the failing tests**

`tests/YtAudioDownloader.Tests/TestEnvironment.cs`:

```csharp
using System.Runtime.CompilerServices;

namespace YtAudioDownloader.Tests;

internal static class TestEnvironment
{
    // Keeps logs and settings written during tests out of the real %LOCALAPPDATA%.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("YTAUDIO_DATA_DIR", Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", "data"));
}
```

`tests/YtAudioDownloader.Tests/ProcessRunnerTests.cs`:

```csharp
using System.Diagnostics;

namespace YtAudioDownloader.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task Returns_exit_code_and_stderr()
    {
        ProcessResult result = await ProcessRunner.RunAsync(
            "cmd.exe", new[] { "/c", "echo oops 1>&2 & exit 3" }, null, CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("oops", result.StdErr);
    }

    [Fact]
    public async Task Streams_utf8_stdout_lines()
    {
        var lines = new List<string>();

        ProcessResult result = await ProcessRunner.RunAsync(
            "powershell.exe",
            new[] { "-NoProfile", "-Command", "[Console]::OutputEncoding = [Text.Encoding]::UTF8; Write-Output 'привет'; Write-Output 'мир'" },
            lines.Add,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "привет", "мир" }, lines);
    }

    [Fact]
    public async Task Cancellation_kills_the_process()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessRunner.RunAsync(
            "ping.exe", new[] { "-n", "30", "127.0.0.1" }, null, cancellation.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }
}
```

`tests/YtAudioDownloader.Tests/ToolClientTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class ToolClientTests
{
    private static readonly string Missing = Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", "missing");

    [Fact]
    public async Task EnsureReady_without_any_yt_dlp_reports_the_missing_tool()
    {
        var client = new YtDlpClient(Path.Combine(Missing, "local", "yt-dlp.exe"), Path.Combine(Missing, "tools", "yt-dlp.exe"), null);

        ToolMissingException error = await Assert.ThrowsAsync<ToolMissingException>(() => client.EnsureReadyAsync(CancellationToken.None));
        Assert.Equal("yt-dlp.exe", error.ToolName);
    }

    [Fact]
    public async Task Download_without_yt_dlp_reports_the_missing_tool()
    {
        var client = new YtDlpClient(Path.Combine(Missing, "local", "yt-dlp.exe"), Path.Combine(Missing, "tools", "yt-dlp.exe"), null);

        await Assert.ThrowsAsync<ToolMissingException>(() =>
            client.DownloadAudioAsync("https://youtu.be/x", Missing, null, CancellationToken.None));
    }

    [Fact]
    public async Task Convert_without_ffmpeg_reports_the_missing_tool()
    {
        var client = new FfmpegClient(Path.Combine(Missing, "ffmpeg.exe"));

        ToolMissingException error = await Assert.ThrowsAsync<ToolMissingException>(() =>
            client.ConvertAsync(new[] { "-version" }, TimeSpan.FromSeconds(1), null, CancellationToken.None));
        Assert.Equal("ffmpeg.exe", error.ToolName);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test YtAudioDownloader.sln`
Expected: build FAILS (`ProcessRunner`, `ProcessResult`, `YtDlpClient`, `FfmpegClient`, `ToolMissingException` not found).

- [ ] **Step 3: Implement `src/YtAudioDownloader/AppPaths.cs`**

```csharp
namespace YtAudioDownloader;

internal static class AppPaths
{
    // YTAUDIO_DATA_DIR lets tests and the packaged smoke test use a throwaway folder.
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("YTAUDIO_DATA_DIR") is { Length: > 0 } overridden
            ? overridden
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtAudioDownloader");

    public static string BundledToolsDir => Path.Combine(AppContext.BaseDirectory, "tools");
    public static string LogsDir => Path.Combine(DataDir, "logs");
    public static string TempRoot => Path.Combine(Path.GetTempPath(), "YtAudioDownloader");
    public static string DefaultOutputDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "YtAudioDownloader");
}
```

- [ ] **Step 4: Implement `src/YtAudioDownloader/Log.cs`**

```csharp
using System.Globalization;
using System.Text;

namespace YtAudioDownloader;

// One file per day in the data folder; "Copy details" points the user here.
internal static class Log
{
    private static readonly object Gate = new();

    public static string CurrentFile =>
        Path.Combine(AppPaths.LogsDir, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogsDir);
                string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(CurrentFile, $"[{time}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the app.
        }
    }

    public static void DeleteOldFiles(int keep = 14)
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogsDir))
                return;
            foreach (string file in Directory.GetFiles(AppPaths.LogsDir, "*.log").OrderByDescending(f => f, StringComparer.Ordinal).Skip(keep))
                File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
```

- [ ] **Step 5: Implement `src/YtAudioDownloader/ProcessRunner.cs`**

```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace YtAudioDownloader;

internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

internal static class ProcessRunner
{
    // Runs a console tool without a window, streams stdout line by line, and kills it (with children) on cancellation.
    public static async Task<ProcessResult> RunAsync(
        string fileName, IEnumerable<string> arguments, Action<string>? onStdOutLine, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        Log.Write("RUN " + Path.GetFileName(fileName) + " " + string.Join(" ", startInfo.ArgumentList.Select(QuoteForLog)));

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        Task pumpOut = PumpAsync(process.StandardOutput, line =>
        {
            stdout.AppendLine(line);
            onStdOutLine?.Invoke(line);
        });
        Task pumpErr = PumpAsync(process.StandardError, line => stderr.AppendLine(line));

        using (cancellationToken.Register(() => Kill(process)))
        {
            await Task.WhenAll(pumpOut, pumpErr).ConfigureAwait(false);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            Log.Write("CANCELLED " + Path.GetFileName(fileName));
            cancellationToken.ThrowIfCancellationRequested();
        }

        Log.Write($"EXIT {process.ExitCode} {Path.GetFileName(fileName)}" + (stderr.Length > 0 ? Environment.NewLine + stderr : ""));
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is string line)
            onLine(line);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }

    private static string QuoteForLog(string argument) => argument.Contains(' ') ? "\"" + argument + "\"" : argument;
}
```

- [ ] **Step 6: Implement `src/YtAudioDownloader/Errors.cs`**

```csharp
namespace YtAudioDownloader;

internal sealed class ToolMissingException(string toolName, string expectedPath)
    : Exception($"{toolName} was not found at {expectedPath}")
{
    public string ToolName { get; } = toolName;
}

internal sealed class YtDlpException(YtDlpFailure failure, string details)
    : Exception($"yt-dlp failed ({failure})")
{
    public YtDlpFailure Failure { get; } = failure;
    public string Details { get; } = details;
}

internal sealed class FfmpegException(string details) : Exception("ffmpeg failed")
{
    public string Details { get; } = details;
}
```

- [ ] **Step 7: Implement `src/YtAudioDownloader/ToolContracts.cs`**

```csharp
namespace YtAudioDownloader;

internal sealed record DownloadedAudio(string FilePath, string? Title, TimeSpan? Duration);

internal sealed record UpdateOutcome(bool Succeeded, string? Version);

internal interface IAudioDownloader
{
    Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken);

    Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken);
}

internal interface IAudioConverter
{
    Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken);
}
```

- [ ] **Step 8: Implement `src/YtAudioDownloader/YtDlpClient.cs`**

```csharp
using System.ComponentModel;

namespace YtAudioDownloader;

// Runs the app's own self-updating copy of yt-dlp, kept in the data folder so it is always writable.
internal sealed class YtDlpClient(string exePath, string bundledExePath, string? denoPath) : IAudioDownloader
{
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(90);

    public static YtDlpClient CreateDefault()
    {
        string deno = Path.Combine(AppPaths.BundledToolsDir, "deno.exe");
        return new YtDlpClient(
            Path.Combine(AppPaths.DataDir, "yt-dlp.exe"),
            Path.Combine(AppPaths.BundledToolsDir, "yt-dlp.exe"),
            File.Exists(deno) ? deno : null);
    }

    // Returns the working copy's version, restoring it from the bundled copy when it is missing or broken.
    public async Task<string> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (await TryGetVersionAsync(cancellationToken).ConfigureAwait(false) is string version)
            return version;

        if (!File.Exists(bundledExePath))
            throw new ToolMissingException("yt-dlp.exe", bundledExePath);

        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.Copy(bundledExePath, exePath, overwrite: true);
        Log.Write($"Copied bundled yt-dlp to {exePath}");

        return await TryGetVersionAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new ToolMissingException("yt-dlp.exe", exePath);
    }

    public async Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(UpdateTimeout);
        bool succeeded;
        try
        {
            ProcessResult result = await ProcessRunner.RunAsync(exePath, YtDlpCommand.BuildUpdateArguments(), null, timeout.Token)
                .ConfigureAwait(false);
            succeeded = result.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Write("yt-dlp update timed out");
            succeeded = false;
        }
        catch (Win32Exception ex)
        {
            Log.Write("yt-dlp update could not start: " + ex.Message);
            succeeded = false;
        }
        return new UpdateOutcome(succeeded, await TryGetVersionAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
            throw new ToolMissingException("yt-dlp.exe", exePath);

        var info = new YtDlpDownloadInfo();
        ProcessResult result = await ProcessRunner.RunAsync(
            exePath,
            YtDlpCommand.BuildDownloadArguments(url, tempDir, denoPath),
            line =>
            {
                if (YtDlpCommand.ReadLine(line, info) is double fraction)
                    progress?.Report(fraction);
            },
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new YtDlpException(YtDlpCommand.Classify(result.StdErr), result.StdErr);
        if (info.FilePath is null || !File.Exists(info.FilePath))
            throw new YtDlpException(YtDlpFailure.Unknown, "yt-dlp finished without reporting the downloaded file." + Environment.NewLine + result.StdErr);

        return new DownloadedAudio(info.FilePath, info.Title, info.Duration);
    }

    private async Task<string?> TryGetVersionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
            return null;
        try
        {
            ProcessResult result = await ProcessRunner.RunAsync(exePath, new[] { "--version" }, null, cancellationToken).ConfigureAwait(false);
            string version = result.StdOut.Trim();
            return result.ExitCode == 0 && version.Length > 0 ? version : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 9: Implement `src/YtAudioDownloader/FfmpegClient.cs`**

```csharp
namespace YtAudioDownloader;

internal sealed class FfmpegClient(string exePath) : IAudioConverter
{
    public static FfmpegClient CreateDefault() => new(Path.Combine(AppPaths.BundledToolsDir, "ffmpeg.exe"));

    public async Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(exePath))
            throw new ToolMissingException("ffmpeg.exe", exePath);

        ProcessResult result = await ProcessRunner.RunAsync(
            exePath,
            arguments,
            line =>
            {
                if (FfmpegCommand.TryParseProgress(line, clipLength) is double fraction)
                    progress?.Report(fraction);
            },
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new FfmpegException(result.StdErr);
    }
}
```

- [ ] **Step 10: Run tests to verify they pass**

Run: `dotnet test YtAudioDownloader.sln`
Expected: all tests PASS, 0 failed. `Cancellation_kills_the_process` finishes in well under 10 s.

- [ ] **Step 11: Commit**

```powershell
git add src/YtAudioDownloader/AppPaths.cs src/YtAudioDownloader/Log.cs src/YtAudioDownloader/ProcessRunner.cs src/YtAudioDownloader/Errors.cs src/YtAudioDownloader/ToolContracts.cs src/YtAudioDownloader/YtDlpClient.cs src/YtAudioDownloader/FfmpegClient.cs tests/YtAudioDownloader.Tests/TestEnvironment.cs tests/YtAudioDownloader.Tests/ProcessRunnerTests.cs tests/YtAudioDownloader.Tests/ToolClientTests.cs
git commit -m "Add process runner, logging and yt-dlp/ffmpeg clients" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: ClipService orchestration and headless `--clip` mode

**Files:**
- Create: `src/YtAudioDownloader/ClipService.cs`, `src/YtAudioDownloader/HeadlessClip.cs`
- Modify: `src/YtAudioDownloader/Errors.cs` (append `ClipOutOfRangeException`), `src/YtAudioDownloader/Program.cs` (full replacement)
- Test: `tests/YtAudioDownloader.Tests/ClipServiceTests.cs`, `tests/YtAudioDownloader.Tests/HeadlessClipTests.cs`

**Interfaces:**
- Consumes: `IAudioDownloader`, `IAudioConverter`, `DownloadedAudio`, `UpdateOutcome`, `YtDlpException`, `YtDlpFailure`, `AppPaths.TempRoot`, `Log.Write`, `YtDlpClient.CreateDefault/EnsureReadyAsync/UpdateAsync`, `FfmpegClient.CreateDefault` (Task 4); `OutputNaming`, `TimeInput` (Task 2); `FfmpegCommand` (Task 3).
- Produces:
  - `internal sealed record ClipRequest(string Url, TimeSpan Start, TimeSpan End, bool Fade, double FadeSeconds, bool Normalize, string OutputFolder)`
  - `internal sealed record ClipResult(string OutputPath, string? Title)`
  - `internal enum ClipStage { Updating, Downloading, Converting }`; `internal sealed record ClipProgress(ClipStage Stage, double? Fraction)` (`Fraction` null = indeterminate)
  - `internal sealed class ClipService(IAudioDownloader downloader, IAudioConverter converter)` — `Task<ClipResult> CreateClipAsync(ClipRequest request, IProgress<ClipProgress>? progress, CancellationToken cancellationToken)`
  - `internal sealed class ClipOutOfRangeException(TimeSpan videoDuration)` with `VideoDuration`
  - `internal static class HeadlessClip` — `int Run(string[] args)`, `bool TryParseArguments(string[] args, out ClipRequest? request, out string? error)`
  - `Program.Main(string[] args)` returns `int`; `YtAudioDownloader.exe --clip <url> <start> <end> <output folder>` prints `OK <path>` (exit 0) or `ERROR ...` (exit 1; bad arguments exit 2).

- [ ] **Step 1: Write the failing tests**

`tests/YtAudioDownloader.Tests/ClipServiceTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public sealed class ClipServiceTests : IDisposable
{
    private readonly string _outputFolder = Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeDownloader _downloader = new();
    private readonly FakeConverter _converter = new();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_outputFolder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Saves_the_clip_named_after_the_video()
    {
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.Equal(Path.Combine(_outputFolder, "Song (1m35s-2m05s).mp3"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        AssertOption("-ss", "95");
        AssertOption("-t", "30");
    }

    [Fact]
    public async Task Updates_yt_dlp_and_retries_once_when_blocked()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Blocked));
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(2, _downloader.DownloadCalls);
        Assert.Equal(1, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Gives_up_after_the_single_retry()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Blocked));
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Blocked));

        YtDlpException error = await Assert.ThrowsAsync<YtDlpException>(() => CreateClip(start: 95, end: 125));

        Assert.Equal(YtDlpFailure.Blocked, error.Failure);
        Assert.Equal(2, _downloader.DownloadCalls);
        Assert.Equal(1, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Does_not_retry_an_unavailable_video()
    {
        _downloader.Results.Enqueue(Fail(YtDlpFailure.Unavailable));

        await Assert.ThrowsAsync<YtDlpException>(() => CreateClip(start: 95, end: 125));

        Assert.Equal(1, _downloader.DownloadCalls);
        Assert.Equal(0, _downloader.UpdateCalls);
    }

    [Fact]
    public async Task Clamps_the_end_to_the_video_length()
    {
        _downloader.Results.Enqueue(Audio(durationSeconds: 205));

        ClipResult result = await CreateClip(start: 190, end: 220);

        Assert.EndsWith("Song (3m10s-3m25s).mp3", result.OutputPath);
        AssertOption("-t", "15");
    }

    [Fact]
    public async Task Rejects_a_start_after_the_end_of_the_video()
    {
        _downloader.Results.Enqueue(Audio(durationSeconds: 205));

        ClipOutOfRangeException error = await Assert.ThrowsAsync<ClipOutOfRangeException>(() => CreateClip(start: 210, end: 240));

        Assert.Equal(TimeSpan.FromSeconds(205), error.VideoDuration);
    }

    [Fact]
    public async Task Never_overwrites_an_existing_file()
    {
        Directory.CreateDirectory(_outputFolder);
        File.WriteAllText(Path.Combine(_outputFolder, "Song (1m35s-2m05s).mp3"), "older clip");
        _downloader.Results.Enqueue(Audio());

        ClipResult result = await CreateClip(start: 95, end: 125);

        Assert.Equal(Path.Combine(_outputFolder, "Song (1m35s-2m05s) (2).mp3"), result.OutputPath);
    }

    [Fact]
    public async Task Cancelling_removes_the_partial_file_and_the_temp_folder()
    {
        _downloader.Results.Enqueue(Audio());
        _converter.Throw = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateClip(start: 95, end: 125));

        Assert.Empty(Directory.GetFiles(_outputFolder));
        Assert.False(Directory.Exists(_downloader.LastTempDir));
    }

    [Fact]
    public async Task Removes_the_temp_folder_after_success()
    {
        _downloader.Results.Enqueue(Audio());

        await CreateClip(start: 95, end: 125);

        Assert.False(Directory.Exists(_downloader.LastTempDir));
    }

    private Task<ClipResult> CreateClip(double start, double end) =>
        new ClipService(_downloader, _converter).CreateClipAsync(
            new ClipRequest("https://youtu.be/x", TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end),
                Fade: true, FadeSeconds: 3, Normalize: true, OutputFolder: _outputFolder),
            progress: null,
            CancellationToken.None);

    private void AssertOption(string name, string value)
    {
        IReadOnlyList<string> args = _converter.Arguments!;
        int index = args.ToList().IndexOf(name);
        Assert.True(index >= 0, $"{name} missing");
        Assert.Equal(value, args[index + 1]);
    }

    private static Func<string, DownloadedAudio> Audio(double durationSeconds = 205) => tempDir =>
    {
        string path = Path.Combine(tempDir, "download.m4a");
        File.WriteAllText(path, "audio");
        return new DownloadedAudio(path, "Song", TimeSpan.FromSeconds(durationSeconds));
    };

    private static Func<string, DownloadedAudio> Fail(YtDlpFailure failure) =>
        _ => throw new YtDlpException(failure, "ERROR: simulated " + failure);

    private sealed class FakeDownloader : IAudioDownloader
    {
        public Queue<Func<string, DownloadedAudio>> Results { get; } = new();
        public int DownloadCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public string? LastTempDir { get; private set; }

        public Task<DownloadedAudio> DownloadAudioAsync(string url, string tempDir, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            LastTempDir = tempDir;
            return Task.FromResult(Results.Dequeue()(tempDir));
        }

        public Task<UpdateOutcome> UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateCalls++;
            return Task.FromResult(new UpdateOutcome(true, "2026.09.01"));
        }
    }

    private sealed class FakeConverter : IAudioConverter
    {
        public IReadOnlyList<string>? Arguments { get; private set; }
        public Exception? Throw { get; set; }

        public Task ConvertAsync(IReadOnlyList<string> arguments, TimeSpan clipLength, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            File.WriteAllText(arguments[^1], "mp3");
            if (Throw != null)
                throw Throw;
            return Task.CompletedTask;
        }
    }
}
```

`tests/YtAudioDownloader.Tests/HeadlessClipTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class HeadlessClipTests
{
    [Fact]
    public void TryParseArguments_builds_a_request_with_default_options()
    {
        Assert.True(HeadlessClip.TryParseArguments(new[] { "https://youtu.be/x", "1:00", "1:30", @"C:\out" }, out ClipRequest? request, out _));

        Assert.Equal(new ClipRequest("https://youtu.be/x", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90), true, 3, true, @"C:\out"), request);
    }

    [Theory]
    [InlineData("https://youtu.be/x", "1:00", "1:30")]
    [InlineData("https://youtu.be/x", "abc", "1:30", @"C:\out")]
    [InlineData("https://youtu.be/x", "1:30", "1:00", @"C:\out")]
    public void TryParseArguments_rejects_bad_input(params string[] args)
    {
        Assert.False(HeadlessClip.TryParseArguments(args, out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test YtAudioDownloader.sln`
Expected: build FAILS (`ClipService`, `ClipRequest`, `ClipResult`, `ClipOutOfRangeException`, `HeadlessClip` not found).

- [ ] **Step 3: Append to `src/YtAudioDownloader/Errors.cs`**

```csharp

internal sealed class ClipOutOfRangeException(TimeSpan videoDuration)
    : Exception($"The start time is after the end of the video ({videoDuration})")
{
    public TimeSpan VideoDuration { get; } = videoDuration;
}
```

- [ ] **Step 4: Implement `src/YtAudioDownloader/ClipService.cs`**

```csharp
namespace YtAudioDownloader;

internal sealed record ClipRequest(string Url, TimeSpan Start, TimeSpan End, bool Fade, double FadeSeconds, bool Normalize, string OutputFolder);

internal sealed record ClipResult(string OutputPath, string? Title);

internal enum ClipStage { Updating, Downloading, Converting }

// Fraction is null while the stage has no measurable progress.
internal sealed record ClipProgress(ClipStage Stage, double? Fraction);

// Download → (if YouTube blocked it: update yt-dlp and retry once) → cut, fade, normalize → MP3.
internal sealed class ClipService(IAudioDownloader downloader, IAudioConverter converter)
{
    public async Task<ClipResult> CreateClipAsync(ClipRequest request, IProgress<ClipProgress>? progress, CancellationToken cancellationToken)
    {
        string tempDir = Path.Combine(AppPaths.TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string? outputPath = null;
        try
        {
            DownloadedAudio audio = await DownloadWithRetryAsync(request.Url, tempDir, progress, cancellationToken).ConfigureAwait(false);

            TimeSpan end = request.End;
            if (audio.Duration is TimeSpan duration)
            {
                if (request.Start >= duration)
                    throw new ClipOutOfRangeException(duration);
                if (end > duration)
                    end = duration;
            }
            TimeSpan clipLength = end - request.Start;

            Directory.CreateDirectory(request.OutputFolder);
            outputPath = OutputNaming.GetAvailablePath(request.OutputFolder, OutputNaming.BuildFileName(audio.Title, request.Start, end));
            string? filter = FfmpegCommand.BuildFilter(clipLength, request.Fade, request.FadeSeconds, request.Normalize);

            progress?.Report(new ClipProgress(ClipStage.Converting, 0));
            await converter.ConvertAsync(
                FfmpegCommand.BuildArguments(audio.FilePath, outputPath, request.Start, clipLength, filter),
                clipLength,
                StageProgress(progress, ClipStage.Converting),
                cancellationToken).ConfigureAwait(false);

            Log.Write("Saved " + outputPath);
            return new ClipResult(outputPath, audio.Title);
        }
        catch when (outputPath != null)
        {
            TryDelete(() => File.Delete(outputPath));
            throw;
        }
        finally
        {
            TryDelete(() => Directory.Delete(tempDir, recursive: true));
        }
    }

    private async Task<DownloadedAudio> DownloadWithRetryAsync(
        string url, string tempDir, IProgress<ClipProgress>? progress, CancellationToken cancellationToken)
    {
        IProgress<double>? downloadProgress = StageProgress(progress, ClipStage.Downloading);
        progress?.Report(new ClipProgress(ClipStage.Downloading, null));
        try
        {
            return await downloader.DownloadAudioAsync(url, tempDir, downloadProgress, cancellationToken).ConfigureAwait(false);
        }
        catch (YtDlpException ex) when (ex.Failure is YtDlpFailure.Blocked or YtDlpFailure.Unknown)
        {
            Log.Write($"Download failed ({ex.Failure}); updating yt-dlp and retrying once.");
            progress?.Report(new ClipProgress(ClipStage.Updating, null));
            await downloader.UpdateAsync(cancellationToken).ConfigureAwait(false);

            foreach (string file in Directory.GetFiles(tempDir))
                File.Delete(file);
            progress?.Report(new ClipProgress(ClipStage.Downloading, null));
            return await downloader.DownloadAudioAsync(url, tempDir, downloadProgress, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IProgress<double>? StageProgress(IProgress<ClipProgress>? progress, ClipStage stage) =>
        progress is null ? null : new ActionProgress<double>(fraction => progress.Report(new ClipProgress(stage, fraction)));

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class ActionProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
```

- [ ] **Step 5: Implement `src/YtAudioDownloader/HeadlessClip.cs`**

```csharp
using System.Diagnostics.CodeAnalysis;

namespace YtAudioDownloader;

// "YtAudioDownloader.exe --clip <url> <start> <end> <output folder>": the full pipeline without the window,
// used to smoke-test a packaged build. Prints "OK <path>" or "ERROR ..." to stdout.
internal static class HeadlessClip
{
    public static int Run(string[] args)
    {
        if (!TryParseArguments(args, out ClipRequest? request, out string? error))
        {
            Console.WriteLine("ERROR " + error);
            return 2;
        }

        try
        {
            YtDlpClient ytDlp = YtDlpClient.CreateDefault();
            ytDlp.EnsureReadyAsync(CancellationToken.None).GetAwaiter().GetResult();
            UpdateOutcome update = ytDlp.UpdateAsync(CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine($"yt-dlp {update.Version} (update {(update.Succeeded ? "ok" : "failed")})");

            ClipResult result = new ClipService(ytDlp, FfmpegClient.CreateDefault())
                .CreateClipAsync(request, null, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("OK " + result.OutputPath);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Write("Headless clip failed: " + ex);
            Console.WriteLine("ERROR " + ex.Message + (ex is YtDlpException ytDlpError ? Environment.NewLine + ytDlpError.Details : ""));
            return 1;
        }
    }

    public static bool TryParseArguments(string[] args, [NotNullWhen(true)] out ClipRequest? request, [NotNullWhen(false)] out string? error)
    {
        request = null;
        if (args.Length != 4)
        {
            error = "usage: --clip <url> <start> <end> <output folder>";
            return false;
        }
        if (!TimeInput.TryParse(args[1], out TimeSpan start))
        {
            error = "bad start time: " + args[1];
            return false;
        }
        if (!TimeInput.TryParse(args[2], out TimeSpan end) || end <= start)
        {
            error = "bad end time: " + args[2];
            return false;
        }

        error = null;
        request = new ClipRequest(args[0], start, end, Fade: true, FadeSeconds: 3, Normalize: true, OutputFolder: args[3]);
        return true;
    }
}
```

- [ ] **Step 6: Replace `src/YtAudioDownloader/Program.cs`**

```csharp
namespace YtAudioDownloader;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--clip")
            return HeadlessClip.Run(args[1..]);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test YtAudioDownloader.sln`
Expected: all tests PASS, 0 failed.

- [ ] **Step 8: Commit**

```powershell
git add src/YtAudioDownloader/ClipService.cs src/YtAudioDownloader/HeadlessClip.cs src/YtAudioDownloader/Errors.cs src/YtAudioDownloader/Program.cs tests/YtAudioDownloader.Tests/ClipServiceTests.cs tests/YtAudioDownloader.Tests/HeadlessClipTests.cs
git commit -m "Add clip pipeline with update-and-retry and headless --clip mode" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: The window — Russian/English texts, settings, new MainForm

**Files:**
- Create: `src/YtAudioDownloader/Strings.cs`, `src/YtAudioDownloader/ErrorText.cs`, `src/YtAudioDownloader/Settings.cs`, `src/YtAudioDownloader/MainForm.Layout.cs`, `scripts/screenshot.ps1`
- Replace: `src/YtAudioDownloader/MainForm.cs`
- Delete: `src/YtAudioDownloader/MainForm.Designer.cs`
- Modify: `src/YtAudioDownloader/YtAudioDownloader.csproj` (add `ApplicationHighDpiMode`)
- Test: `tests/YtAudioDownloader.Tests/StringsTests.cs`, `tests/YtAudioDownloader.Tests/ErrorTextTests.cs`, `tests/YtAudioDownloader.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–5 (`TimeInput`, `YouTubeUrl`, `ClipService`, `ClipRequest`, `ClipResult`, `ClipProgress`, `ClipStage`, `YtDlpClient`, `FfmpegClient`, `UpdateOutcome`, `AppPaths`, `Log`, exceptions).
- Produces:
  - `internal sealed record UiText` (all window texts, `required` init properties); `internal static class Strings` — `UiText English`, `UiText Russian`, `UiText Current`, `UiText ForLanguage(string twoLetterCode)`.
  - `internal static class ErrorText` — `(string Message, string Details) Describe(Exception error, UiText text)`.
  - `internal sealed class Settings` — `string? OutputFolder`, `static string DefaultPath`, `static Settings Load(string path)`, `void Save(string path)`.
  - `public partial class MainForm : Form` with a parameterless constructor.

- [ ] **Step 1: Write the failing tests**

`tests/YtAudioDownloader.Tests/StringsTests.cs`:

```csharp
using System.Reflection;

namespace YtAudioDownloader.Tests;

public class StringsTests
{
    private static readonly PropertyInfo[] Texts = typeof(UiText).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string))
        .ToArray();

    [Theory]
    [InlineData("ru", true)]
    [InlineData("RU", true)]
    [InlineData("en", false)]
    [InlineData("de", false)]
    public void ForLanguage_picks_russian_only_for_russian(string code, bool russian)
    {
        Assert.Same(russian ? Strings.Russian : Strings.English, Strings.ForLanguage(code));
    }

    [Fact]
    public void Every_text_is_filled_in_both_languages()
    {
        Assert.NotEmpty(Texts);
        foreach (PropertyInfo property in Texts)
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(Strings.English)), "English " + property.Name);
            Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(Strings.Russian)), "Russian " + property.Name);
        }
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        foreach (PropertyInfo property in Texts)
        {
            bool english = ((string)property.GetValue(Strings.English)!).Contains("{0}");
            bool russian = ((string)property.GetValue(Strings.Russian)!).Contains("{0}");
            Assert.True(english == russian, property.Name);
        }
    }
}
```

`tests/YtAudioDownloader.Tests/ErrorTextTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public class ErrorTextTests
{
    private static readonly UiText Text = Strings.English;

    [Theory]
    [InlineData(YtDlpFailure.Blocked)]
    [InlineData(YtDlpFailure.Unavailable)]
    [InlineData(YtDlpFailure.InvalidUrl)]
    [InlineData(YtDlpFailure.Network)]
    [InlineData(YtDlpFailure.Unknown)]
    public void Yt_dlp_failures_get_their_own_message_and_keep_the_raw_output(YtDlpFailure failure)
    {
        (string message, string details) = ErrorText.Describe(new YtDlpException(failure, "raw stderr"), Text);

        string expected = failure switch
        {
            YtDlpFailure.Blocked => Text.ErrorBlocked,
            YtDlpFailure.Unavailable => Text.ErrorUnavailable,
            YtDlpFailure.InvalidUrl => Text.ErrorInvalidUrl,
            YtDlpFailure.Network => Text.ErrorNetwork,
            _ => Text.ErrorUnknown,
        };
        Assert.Equal(expected, message);
        Assert.Equal("raw stderr", details);
    }

    [Fact]
    public void Missing_tool_names_the_file()
    {
        (string message, _) = ErrorText.Describe(new ToolMissingException("ffmpeg.exe", @"C:\app\tools\ffmpeg.exe"), Text);

        Assert.Contains("ffmpeg.exe", message);
    }

    [Fact]
    public void Out_of_range_shows_the_video_length()
    {
        (string message, _) = ErrorText.Describe(new ClipOutOfRangeException(TimeSpan.FromSeconds(205)), Text);

        Assert.Contains("3:25", message);
    }

    [Fact]
    public void Ffmpeg_failure_keeps_its_output()
    {
        (string message, string details) = ErrorText.Describe(new FfmpegException("ffmpeg said no"), Text);

        Assert.Equal(Text.ErrorConvert, message);
        Assert.Equal("ffmpeg said no", details);
    }

    [Fact]
    public void Anything_else_is_unknown_with_the_full_exception()
    {
        (string message, string details) = ErrorText.Describe(new InvalidOperationException("boom"), Text);

        Assert.Equal(Text.ErrorUnknown, message);
        Assert.Contains("boom", details);
    }
}
```

`tests/YtAudioDownloader.Tests/SettingsTests.cs`:

```csharp
namespace YtAudioDownloader.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "YtAudioDownloader.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsFile => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Saved_settings_load_back()
    {
        new Settings { OutputFolder = @"D:\Музыка" }.Save(SettingsFile);

        Assert.Equal(@"D:\Музыка", Settings.Load(SettingsFile).OutputFolder);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        Assert.Null(Settings.Load(SettingsFile).OutputFolder);
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, "{ not json");

        Assert.Null(Settings.Load(SettingsFile).OutputFolder);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test YtAudioDownloader.sln`
Expected: build FAILS (`UiText`, `Strings`, `ErrorText`, `Settings` not found).

- [ ] **Step 3: Implement `src/YtAudioDownloader/Strings.cs`**

```csharp
using System.Globalization;

namespace YtAudioDownloader;

// Every text the window shows. "required" makes a missing translation a compile error.
internal sealed record UiText
{
    public required string WindowTitle { get; init; }
    public required string UrlLabel { get; init; }
    public required string StartLabel { get; init; }
    public required string EndLabel { get; init; }
    public required string TimeHint { get; init; }
    public required string FadeCheckBox { get; init; }
    public required string NormalizeCheckBox { get; init; }
    public required string OutputFolderLabel { get; init; }
    public required string BrowseButton { get; init; }
    public required string FolderDialogDescription { get; init; }
    public required string DownloadButton { get; init; }
    public required string CancelButton { get; init; }
    public required string OpenFolderButton { get; init; }
    public required string StatusReady { get; init; }
    public required string StatusUpdating { get; init; }
    public required string StatusDownloading { get; init; }
    public required string StatusConverting { get; init; }
    public required string StatusDone { get; init; }
    public required string StatusCancelled { get; init; }
    public required string StatusError { get; init; }
    public required string ToolChecking { get; init; }
    public required string ToolOk { get; init; }
    public required string ToolUpdateFailed { get; init; }
    public required string ToolMissing { get; init; }
    public required string ErrorDialogTitle { get; init; }
    public required string CopyDetailsButton { get; init; }
    public required string Copied { get; init; }
    public required string DetailsExpander { get; init; }
    public required string ErrorNoUrl { get; init; }
    public required string ErrorBadStart { get; init; }
    public required string ErrorBadEnd { get; init; }
    public required string ErrorEndBeforeStart { get; init; }
    public required string ErrorBlocked { get; init; }
    public required string ErrorUnavailable { get; init; }
    public required string ErrorInvalidUrl { get; init; }
    public required string ErrorNetwork { get; init; }
    public required string ErrorToolMissing { get; init; }
    public required string ErrorOutOfRange { get; init; }
    public required string ErrorConvert { get; init; }
    public required string ErrorUnknown { get; init; }
}

internal static class Strings
{
    public static UiText English { get; } = new()
    {
        WindowTitle = "YouTube Audio Downloader",
        UrlLabel = "YouTube link",
        StartLabel = "Start",
        EndLabel = "End",
        TimeHint = "e.g. 1:35 or 95 (seconds)",
        FadeCheckBox = "Fade in/out, seconds:",
        NormalizeCheckBox = "Even out the volume",
        OutputFolderLabel = "Save to folder",
        BrowseButton = "Browse...",
        FolderDialogDescription = "Where to save the MP3 files",
        DownloadButton = "Download",
        CancelButton = "Cancel",
        OpenFolderButton = "Open folder",
        StatusReady = "Ready.",
        StatusUpdating = "Updating yt-dlp...",
        StatusDownloading = "Downloading audio...",
        StatusConverting = "Cutting and converting...",
        StatusDone = "Saved: {0}",
        StatusCancelled = "Cancelled.",
        StatusError = "Error.",
        ToolChecking = "yt-dlp: checking for updates...",
        ToolOk = "yt-dlp {0}",
        ToolUpdateFailed = "yt-dlp {0} · update check failed",
        ToolMissing = "yt-dlp not found",
        ErrorDialogTitle = "Couldn't save the clip",
        CopyDetailsButton = "Copy details",
        Copied = "Copied",
        DetailsExpander = "Details",
        ErrorNoUrl = "Paste a YouTube link first.",
        ErrorBadStart = "Can't read the start time. Examples: 1:35 or 95.",
        ErrorBadEnd = "Can't read the end time. Examples: 2:05 or 125.",
        ErrorEndBeforeStart = "The end must be later than the start.",
        ErrorBlocked = "YouTube blocked the download. Try again in a few minutes. If it keeps happening, click \"Copy details\" and send the text to whoever set up this app for you.",
        ErrorUnavailable = "This video can't be downloaded: it may be private, removed, age-restricted, or not started yet.",
        ErrorInvalidUrl = "This doesn't look like a YouTube video link.",
        ErrorNetwork = "Can't reach YouTube. Check your internet connection.",
        ErrorToolMissing = "Some program files are missing ({0}). Download the app again and unpack the whole archive.",
        ErrorOutOfRange = "The start time is after the end of the video ({0}).",
        ErrorConvert = "Couldn't cut or convert the audio.",
        ErrorUnknown = "Something went wrong.",
    };

    public static UiText Russian { get; } = new()
    {
        WindowTitle = "YouTube Audio Downloader",
        UrlLabel = "Ссылка на YouTube",
        StartLabel = "Начало",
        EndLabel = "Конец",
        TimeHint = "например 1:35 или 95 (секунды)",
        FadeCheckBox = "Плавное появление и затухание, сек:",
        NormalizeCheckBox = "Выровнять громкость",
        OutputFolderLabel = "Папка для сохранения",
        BrowseButton = "Обзор...",
        FolderDialogDescription = "Куда сохранять MP3-файлы",
        DownloadButton = "Скачать",
        CancelButton = "Отмена",
        OpenFolderButton = "Открыть папку",
        StatusReady = "Готово к работе.",
        StatusUpdating = "Обновление yt-dlp...",
        StatusDownloading = "Скачивание аудио...",
        StatusConverting = "Обрезка и конвертация...",
        StatusDone = "Сохранено: {0}",
        StatusCancelled = "Отменено.",
        StatusError = "Ошибка.",
        ToolChecking = "yt-dlp: проверка обновлений...",
        ToolOk = "yt-dlp {0}",
        ToolUpdateFailed = "yt-dlp {0} · не удалось проверить обновления",
        ToolMissing = "yt-dlp не найден",
        ErrorDialogTitle = "Не удалось сохранить фрагмент",
        CopyDetailsButton = "Скопировать подробности",
        Copied = "Скопировано",
        DetailsExpander = "Подробности",
        ErrorNoUrl = "Сначала вставьте ссылку на YouTube.",
        ErrorBadStart = "Не удалось прочитать время начала. Примеры: 1:35 или 95.",
        ErrorBadEnd = "Не удалось прочитать время конца. Примеры: 2:05 или 125.",
        ErrorEndBeforeStart = "Конец должен быть позже начала.",
        ErrorBlocked = "YouTube заблокировал скачивание. Попробуйте ещё раз через несколько минут. Если ошибка повторяется, нажмите «Скопировать подробности» и отправьте текст тому, кто установил вам эту программу.",
        ErrorUnavailable = "Это видео нельзя скачать: возможно, оно закрыто, удалено, с возрастным ограничением или ещё не началось.",
        ErrorInvalidUrl = "Это не похоже на ссылку на видео YouTube.",
        ErrorNetwork = "Нет связи с YouTube. Проверьте подключение к интернету.",
        ErrorToolMissing = "Не хватает файлов программы ({0}). Скачайте программу заново и распакуйте архив целиком.",
        ErrorOutOfRange = "Время начала больше длины видео ({0}).",
        ErrorConvert = "Не удалось обрезать или сконвертировать аудио.",
        ErrorUnknown = "Что-то пошло не так.",
    };

    // YTAUDIO_LANG=ru|en overrides the Windows display language (useful for screenshots and testing).
    public static UiText Current { get; } = ForLanguage(
        Environment.GetEnvironmentVariable("YTAUDIO_LANG") is { Length: > 0 } language
            ? language
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static UiText ForLanguage(string twoLetterCode) =>
        string.Equals(twoLetterCode, "ru", StringComparison.OrdinalIgnoreCase) ? Russian : English;
}
```

- [ ] **Step 4: Implement `src/YtAudioDownloader/ErrorText.cs`**

```csharp
namespace YtAudioDownloader;

internal static class ErrorText
{
    // The short message for the error dialog, plus the technical details behind "Copy details".
    public static (string Message, string Details) Describe(Exception error, UiText text) => error switch
    {
        YtDlpException { Failure: YtDlpFailure.Blocked } e => (text.ErrorBlocked, e.Details),
        YtDlpException { Failure: YtDlpFailure.Unavailable } e => (text.ErrorUnavailable, e.Details),
        YtDlpException { Failure: YtDlpFailure.InvalidUrl } e => (text.ErrorInvalidUrl, e.Details),
        YtDlpException { Failure: YtDlpFailure.Network } e => (text.ErrorNetwork, e.Details),
        YtDlpException e => (text.ErrorUnknown, e.Details),
        ToolMissingException e => (string.Format(text.ErrorToolMissing, e.ToolName), e.Message),
        ClipOutOfRangeException e => (string.Format(text.ErrorOutOfRange, TimeInput.FormatDisplay(e.VideoDuration)), e.Message),
        FfmpegException e => (text.ErrorConvert, e.Details),
        _ => (text.ErrorUnknown, error.ToString()),
    };
}
```

- [ ] **Step 5: Implement `src/YtAudioDownloader/Settings.cs`**

```csharp
using System.Text.Json;

namespace YtAudioDownloader;

// What the app remembers between runs.
internal sealed class Settings
{
    public string? OutputFolder { get; set; }

    public static string DefaultPath => Path.Combine(AppPaths.DataDir, "settings.json");

    public static Settings Load(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Could not save settings: " + ex.Message);
        }
    }
}
```

- [ ] **Step 6: Run the new unit tests**

Run: `dotnet test YtAudioDownloader.sln`
Expected: all tests PASS (the old `MainForm` still compiles at this point).

- [ ] **Step 7: Delete the old designer file and add the HiDPI setting**

```powershell
git rm src/YtAudioDownloader/MainForm.Designer.cs
```

In `src/YtAudioDownloader/YtAudioDownloader.csproj`, add inside the existing `PropertyGroup`:

```xml
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
```

- [ ] **Step 8: Create `src/YtAudioDownloader/MainForm.Layout.cs`**

```csharp
namespace YtAudioDownloader;

// The window is laid out in code (no designer file) so it can grow for longer Russian texts.
public partial class MainForm
{
    private const int ContentWidth = 520;

    private readonly TextBox urlTextBox = new() { Width = ContentWidth, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox startTextBox = new() { Width = 90, Text = "0:00" };
    private readonly TextBox endTextBox = new() { Width = 90, Text = "0:30" };
    private readonly CheckBox fadeCheckBox = new() { AutoSize = true, Checked = true, Anchor = AnchorStyles.Left };
    private readonly NumericUpDown fadeSecondsUpDown = new()
    {
        Width = 60, Minimum = 0.5m, Maximum = 10m, Increment = 0.5m, DecimalPlaces = 1, Value = 3m,
    };
    private readonly CheckBox normalizeCheckBox = new() { AutoSize = true, Checked = true };
    private readonly TextBox outputFolderTextBox = new() { Dock = DockStyle.Fill };
    private readonly Button browseButton = new() { AutoSize = true };
    private readonly Button downloadButton = new() { AutoSize = true, MinimumSize = new Size(110, 32) };
    private readonly Button cancelButton = new() { AutoSize = true, Enabled = false, MinimumSize = new Size(0, 32) };
    private readonly Button openFolderButton = new() { AutoSize = true, Visible = false, MinimumSize = new Size(0, 32) };
    private readonly ProgressBar progressBar = new()
    {
        Width = ContentWidth, Height = 18, Maximum = 1000, Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(ContentWidth, 0) };
    private readonly Label toolStatusLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

    private void InitializeLayout()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Font = SystemFonts.MessageBoxFont ?? Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };

        root.Controls.Add(Caption(_ui.UrlLabel));
        root.Controls.Add(urlTextBox);

        var times = new TableLayoutPanel { ColumnCount = 3, RowCount = 2, AutoSize = true, Margin = Padding.Empty };
        times.Controls.Add(Caption(_ui.StartLabel), 0, 0);
        times.Controls.Add(Caption(_ui.EndLabel), 1, 0);
        times.Controls.Add(startTextBox, 0, 1);
        times.Controls.Add(endTextBox, 1, 1);
        times.Controls.Add(new Label { Text = _ui.TimeHint, AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 2, 1);
        root.Controls.Add(times);

        var fadeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
        fadeCheckBox.Text = _ui.FadeCheckBox;
        fadeRow.Controls.Add(fadeCheckBox);
        fadeRow.Controls.Add(fadeSecondsUpDown);
        root.Controls.Add(fadeRow);

        normalizeCheckBox.Text = _ui.NormalizeCheckBox;
        root.Controls.Add(normalizeCheckBox);

        root.Controls.Add(Caption(_ui.OutputFolderLabel));
        var outputRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        browseButton.Text = _ui.BrowseButton;
        outputRow.Controls.Add(outputFolderTextBox, 0, 0);
        outputRow.Controls.Add(browseButton, 1, 0);
        root.Controls.Add(outputRow);

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 4) };
        downloadButton.Text = _ui.DownloadButton;
        cancelButton.Text = _ui.CancelButton;
        openFolderButton.Text = _ui.OpenFolderButton;
        actions.Controls.AddRange(new Control[] { downloadButton, cancelButton, openFolderButton });
        root.Controls.Add(actions);

        root.Controls.Add(progressBar);
        statusLabel.Text = _ui.StatusReady;
        root.Controls.Add(statusLabel);
        root.Controls.Add(toolStatusLabel);

        Controls.Add(root);
        AcceptButton = downloadButton;

        urlTextBox.TextChanged += urlTextBox_TextChanged;
        startTextBox.TextChanged += startTextBox_TextChanged;
        endTextBox.TextChanged += endTextBox_TextChanged;
        fadeCheckBox.CheckedChanged += (_, _) => fadeSecondsUpDown.Enabled = fadeCheckBox.Checked;
        browseButton.Click += browseButton_Click;
        downloadButton.Click += downloadButton_Click;
        cancelButton.Click += (_, _) => _cancellation?.Cancel();
        openFolderButton.Click += openFolderButton_Click;

        ResumeLayout(performLayout: true);
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
}
```

- [ ] **Step 9: Replace `src/YtAudioDownloader/MainForm.cs`**

```csharp
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace YtAudioDownloader;

public partial class MainForm : Form
{
    private static readonly TimeSpan DefaultClipLength = TimeSpan.FromSeconds(30);

    private readonly UiText _ui = Strings.Current;
    private readonly Settings _settings = Settings.Load(Settings.DefaultPath);
    private readonly YtDlpClient _ytDlp = YtDlpClient.CreateDefault();
    private readonly ClipService _clips;
    private Task _ytDlpReady = Task.CompletedTask;
    private string _ytDlpVersion = "?";
    private CancellationTokenSource? _cancellation;
    private string? _lastOutputPath;
    private bool _endEditedByUser;
    private bool _settingEnd;

    public MainForm()
    {
        InitializeLayout();
        _clips = new ClipService(_ytDlp, FfmpegClient.CreateDefault());
        Text = $"{_ui.WindowTitle} {Application.ProductVersion}";
        outputFolderTextBox.Text = string.IsNullOrWhiteSpace(_settings.OutputFolder) ? AppPaths.DefaultOutputDir : _settings.OutputFolder;
        Log.DeleteOldFiles();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ytDlpReady = PrepareYtDlpAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cancellation?.Cancel();
        base.OnFormClosing(e);
    }

    // Startup: make sure yt-dlp works, then update it in the background. Never throws.
    private async Task PrepareYtDlpAsync()
    {
        toolStatusLabel.Text = _ui.ToolChecking;
        try
        {
            _ytDlpVersion = await _ytDlp.EnsureReadyAsync(CancellationToken.None);
            UpdateOutcome update = await _ytDlp.UpdateAsync(CancellationToken.None);
            _ytDlpVersion = update.Version ?? _ytDlpVersion;
            if (!IsDisposed)
                toolStatusLabel.Text = string.Format(update.Succeeded ? _ui.ToolOk : _ui.ToolUpdateFailed, _ytDlpVersion);
        }
        catch (ToolMissingException ex)
        {
            Log.Write(ex.Message);
            if (!IsDisposed)
                toolStatusLabel.Text = _ui.ToolMissing;
        }
        catch (Exception ex)
        {
            Log.Write("yt-dlp preparation failed: " + ex);
            if (!IsDisposed)
                toolStatusLabel.Text = string.Format(_ui.ToolUpdateFailed, _ytDlpVersion);
        }
    }

    private void urlTextBox_TextChanged(object? sender, EventArgs e)
    {
        _endEditedByUser = false; // a new link starts a new clip
        if (YouTubeUrl.TryGetStartTime(urlTextBox.Text) is TimeSpan start)
            startTextBox.Text = TimeInput.FormatDisplay(start);
        SetDefaultEndIfUntouched();
    }

    private void startTextBox_TextChanged(object? sender, EventArgs e) => SetDefaultEndIfUntouched();

    private void endTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_settingEnd)
            _endEditedByUser = true;
    }

    // End follows Start + 30 s until the user types an end of their own.
    private void SetDefaultEndIfUntouched()
    {
        if (_endEditedByUser || !TimeInput.TryParse(startTextBox.Text, out TimeSpan start))
            return;
        _settingEnd = true;
        endTextBox.Text = TimeInput.FormatDisplay(start + DefaultClipLength);
        _settingEnd = false;
    }

    private async void downloadButton_Click(object? sender, EventArgs e)
    {
        if (_cancellation != null || ReadRequest() is not ClipRequest request)
            return;

        _settings.OutputFolder = request.OutputFolder;
        _settings.Save(Settings.DefaultPath);

        _cancellation = new CancellationTokenSource();
        SetRunning(true);
        try
        {
            if (!_ytDlpReady.IsCompleted)
            {
                ShowProgress(new ClipProgress(ClipStage.Updating, null));
                await _ytDlpReady.WaitAsync(_cancellation.Token);
            }

            ClipResult result = await _clips.CreateClipAsync(request, new Progress<ClipProgress>(ShowProgress), _cancellation.Token);
            _lastOutputPath = result.OutputPath;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = progressBar.Maximum;
            statusLabel.Text = string.Format(_ui.StatusDone, Path.GetFileName(result.OutputPath));
            openFolderButton.Visible = true;
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                ResetProgress();
                statusLabel.Text = _ui.StatusCancelled;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Clip failed: " + ex);
            if (!IsDisposed)
            {
                ResetProgress();
                statusLabel.Text = _ui.StatusError;
                ShowError(ex, request);
            }
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            if (!IsDisposed)
                SetRunning(false);
        }
    }

    private ClipRequest? ReadRequest()
    {
        string url = urlTextBox.Text.Trim();
        TimeSpan start = TimeSpan.Zero;
        TimeSpan end = TimeSpan.Zero;
        string? problem = null;

        if (url.Length == 0)
            problem = _ui.ErrorNoUrl;
        else if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            problem = _ui.ErrorInvalidUrl;
        else if (!TimeInput.TryParse(startTextBox.Text, out start))
            problem = _ui.ErrorBadStart;
        else if (!TimeInput.TryParse(endTextBox.Text, out end))
            problem = _ui.ErrorBadEnd;
        else if (end <= start)
            problem = _ui.ErrorEndBeforeStart;

        if (problem != null)
        {
            MessageBox.Show(this, problem, _ui.ErrorDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        string folder = outputFolderTextBox.Text.Trim();
        return new ClipRequest(url, start, end, fadeCheckBox.Checked, (double)fadeSecondsUpDown.Value, normalizeCheckBox.Checked,
            folder.Length > 0 ? folder : AppPaths.DefaultOutputDir);
    }

    private void ShowProgress(ClipProgress progress)
    {
        if (IsDisposed)
            return;

        string stage = progress.Stage switch
        {
            ClipStage.Updating => _ui.StatusUpdating,
            ClipStage.Downloading => _ui.StatusDownloading,
            _ => _ui.StatusConverting,
        };
        if (progress.Fraction is double fraction)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = (int)Math.Round(fraction * progressBar.Maximum);
            statusLabel.Text = stage + " " + fraction.ToString("P0", CultureInfo.CurrentCulture);
        }
        else
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = stage;
        }
    }

    private void ResetProgress()
    {
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = 0;
    }

    private void SetRunning(bool running)
    {
        foreach (Control control in new Control[] { urlTextBox, startTextBox, endTextBox, fadeCheckBox, normalizeCheckBox, outputFolderTextBox, browseButton, downloadButton })
            control.Enabled = !running;
        fadeSecondsUpDown.Enabled = !running && fadeCheckBox.Checked;
        cancelButton.Enabled = running;
        if (running)
            openFolderButton.Visible = false;
    }

    private void ShowError(Exception error, ClipRequest request)
    {
        (string message, string details) = ErrorText.Describe(error, _ui);
        string report = string.Join(Environment.NewLine,
            $"App: {Application.ProductVersion}",
            $"yt-dlp: {_ytDlpVersion}",
            $"URL: {request.Url}",
            $"Clip: {TimeInput.FormatDisplay(request.Start)} - {TimeInput.FormatDisplay(request.End)}",
            $"Log: {Log.CurrentFile}",
            "",
            details.Trim());

        var copyButton = new TaskDialogButton(_ui.CopyDetailsButton) { AllowCloseDialog = false };
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(report);
                copyButton.Text = _ui.Copied;
            }
            catch (ExternalException ex)
            {
                Log.Write("Clipboard is busy: " + ex.Message);
            }
        };

        TaskDialog.ShowDialog(this, new TaskDialogPage
        {
            Caption = _ui.WindowTitle,
            Heading = _ui.ErrorDialogTitle,
            Text = message,
            Icon = TaskDialogIcon.Error,
            Buttons = { copyButton, TaskDialogButton.OK },
            Expander = new TaskDialogExpander(report)
            {
                CollapsedButtonText = _ui.DetailsExpander,
                ExpandedButtonText = _ui.DetailsExpander,
            },
        });
    }

    private void browseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = _ui.FolderDialogDescription,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(outputFolderTextBox.Text) ? outputFolderTextBox.Text : AppPaths.DefaultOutputDir,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            outputFolderTextBox.Text = dialog.SelectedPath;
    }

    private void openFolderButton_Click(object? sender, EventArgs e)
    {
        if (_lastOutputPath != null && File.Exists(_lastOutputPath))
            Process.Start("explorer.exe", $"/select,\"{_lastOutputPath}\"")?.Dispose();
    }
}
```

- [ ] **Step 10: Create `scripts/screenshot.ps1`**

```powershell
# Starts the app, captures its window to a PNG and closes it. Used to check the layout in both languages.
# Usage: scripts\screenshot.ps1 -Exe <path to YtAudioDownloader.exe> -Lang ru -Out window-ru.png
param(
    [Parameter(Mandatory)][string]$Exe,
    [ValidateSet('ru', 'en')][string]$Lang = 'en',
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ScreenshotNative
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@

$env:YTAUDIO_LANG = $Lang
$process = Start-Process -FilePath $Exe -PassThru
try {
    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 300
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'The window did not appear within 15 seconds.' }
    Start-Sleep -Seconds 2   # let the startup status text settle

    $rect = New-Object ScreenshotNative+RECT
    [ScreenshotNative]::GetWindowRect($process.MainWindowHandle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [ScreenshotNative]::PrintWindow($process.MainWindowHandle, $hdc, 2) | Out-Null   # 2 = PW_RENDERFULLCONTENT
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Saved $Out"
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
}
```

- [ ] **Step 11: Build, test, and look at the window in both languages**

Run: `dotnet test YtAudioDownloader.sln`
Expected: all tests PASS, 0 failed, and the build has no warnings from the new files.

Run (from the repo root; `<scratch>` = any temp folder outside the repo):

```powershell
dotnet build src/YtAudioDownloader/YtAudioDownloader.csproj -c Debug
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/screenshot.ps1 -Exe src/YtAudioDownloader/bin/Debug/net10.0-windows/YtAudioDownloader.exe -Lang ru -Out <scratch>/window-ru.png
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/screenshot.ps1 -Exe src/YtAudioDownloader/bin/Debug/net10.0-windows/YtAudioDownloader.exe -Lang en -Out <scratch>/window-en.png
```

Expected: both PNGs show every control from Step 8 in order, no text clipped or overlapping, labels in the requested language, the output folder filled with `...\Music\YtAudioDownloader`, and the bottom line reading "yt-dlp not found" / "yt-dlp не найден" (no tools are bundled in a plain debug build yet — that is fine here). Open the PNGs (Read tool) and describe what you see in the report. Fix layout problems before committing.

- [ ] **Step 12: Commit**

```powershell
git add -A src/YtAudioDownloader tests/YtAudioDownloader.Tests scripts/screenshot.ps1
git commit -m "Rebuild the window: RU/EN texts, progress, cancel, error details, remembered folder" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Packaging, release workflow, docs, and end-to-end check of the zip

**Files:**
- Create: `scripts/fetch-tools.ps1`, `build.ps1`, `build.bat`, `docs/release-notes.md`, `docs/user-readme.txt`
- Replace: `README.md`, `.github/workflows/build.yml`
- Modify: `src/YtAudioDownloader/YtAudioDownloader.csproj` (dev-time tools copy)

**Interfaces:**
- Consumes: the app from Tasks 1–6, `--clip` headless mode and `YTAUDIO_DATA_DIR` (Tasks 4–5), `scripts/screenshot.ps1` (Task 6).
- Produces: `build.ps1 [-Version <semver>]` → `publish/YtAudioDownloader/` and `publish/YtAudioDownloader.zip`; `scripts/fetch-tools.ps1 -Destination <dir>` → `yt-dlp.exe`, `deno.exe`, `ffmpeg.exe`, `licenses/`; tag-triggered GitHub Release with asset `YtAudioDownloader.zip`.

- [ ] **Step 1: Create `scripts/fetch-tools.ps1`**

```powershell
# Downloads the tools the app runs into -Destination: yt-dlp (latest nightly), deno and ffmpeg (pinned) and their license notes.
# Works in Windows PowerShell 5.1 and PowerShell 7. Big pinned downloads are cached in .tools-cache\.
param(
    [Parameter(Mandatory)][string]$Destination,
    [string]$CacheDir = (Join-Path $PSScriptRoot '..\.tools-cache')
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$DenoVersion = 'v2.9.6'
$FfmpegVersion = '8.1'
$YtDlpUrl = 'https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe'
$DenoUrl = "https://github.com/denoland/deno/releases/download/$DenoVersion/deno-x86_64-pc-windows-msvc.zip"
$FfmpegUrl = "https://github.com/GyanD/codexffmpeg/releases/download/$FfmpegVersion/ffmpeg-$FfmpegVersion-essentials_build.zip"

New-Item -ItemType Directory -Force $Destination, $CacheDir | Out-Null
$licenses = Join-Path $Destination 'licenses'
New-Item -ItemType Directory -Force $licenses | Out-Null

function Get-Cached([string]$Url, [string]$FileName) {
    $path = Join-Path $CacheDir $FileName
    if (-not (Test-Path $path)) {
        Write-Host "Downloading $Url"
        Invoke-WebRequest -Uri $Url -OutFile "$path.partial" -UseBasicParsing
        Move-Item "$path.partial" $path -Force
    }
    return $path
}

# yt-dlp: never cached, always the newest nightly (the app keeps its own copy updated afterwards).
Write-Host "Downloading $YtDlpUrl"
Invoke-WebRequest -Uri $YtDlpUrl -OutFile (Join-Path $Destination 'yt-dlp.exe') -UseBasicParsing

$denoZip = Get-Cached $DenoUrl "deno-$DenoVersion.zip"
Expand-Archive $denoZip -DestinationPath $Destination -Force

$ffmpegZip = Get-Cached $FfmpegUrl "ffmpeg-$FfmpegVersion-essentials_build.zip"
$ffmpegDir = Join-Path $CacheDir "ffmpeg-$FfmpegVersion"
if (-not (Test-Path $ffmpegDir)) { Expand-Archive $ffmpegZip -DestinationPath $ffmpegDir -Force }
$ffmpegRoot = Get-ChildItem $ffmpegDir -Directory | Select-Object -First 1
Copy-Item (Join-Path $ffmpegRoot.FullName 'bin\ffmpeg.exe') $Destination -Force
Copy-Item (Join-Path $ffmpegRoot.FullName 'LICENSE') (Join-Path $licenses 'ffmpeg-LICENSE.txt') -Force

@"
Third-party programs in this folder
===================================

yt-dlp (nightly) - The Unlicense (public domain)
  https://github.com/yt-dlp/yt-dlp

Deno $DenoVersion - MIT License
  https://github.com/denoland/deno

FFmpeg $FfmpegVersion essentials build by gyan.dev - GPL v3 (see ffmpeg-LICENSE.txt)
  Build:  https://github.com/GyanD/codexffmpeg/releases/tag/$FfmpegVersion
  Source: https://ffmpeg.org/releases/ffmpeg-$FfmpegVersion.tar.xz
"@ | Set-Content -Path (Join-Path $licenses 'THIRD-PARTY.txt') -Encoding UTF8

foreach ($tool in 'yt-dlp.exe', 'deno.exe', 'ffmpeg.exe') {
    if (-not (Test-Path (Join-Path $Destination $tool))) { throw "$tool is missing after download" }
}
Write-Host "Tools ready in $Destination"
```

- [ ] **Step 2: Create `build.ps1`**

```powershell
# Tests, publishes a self-contained single-file build, adds the tools and zips it:
#   publish\YtAudioDownloader\  and  publish\YtAudioDownloader.zip
param([string]$Version = '0.0.0-dev')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publish = Join-Path $root 'publish'
$appDir = Join-Path $publish 'YtAudioDownloader'
$zip = Join-Path $publish 'YtAudioDownloader.zip'

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet test (Join-Path $root 'YtAudioDownloader.sln') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

dotnet publish (Join-Path $root 'src\YtAudioDownloader\YtAudioDownloader.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:Version=$Version -o $appDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

& (Join-Path $root 'scripts\fetch-tools.ps1') -Destination (Join-Path $appDir 'tools')
Copy-Item (Join-Path $root 'docs\user-readme.txt') (Join-Path $appDir 'README.txt')

Compress-Archive -Path $appDir -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Done: $zip"
```

- [ ] **Step 3: Create `build.bat`**

```bat
@echo off
rem Builds publish\YtAudioDownloader.zip. Needs the .NET 10 SDK. Arguments are passed to build.ps1 (e.g. -Version 1.0.0).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
```

- [ ] **Step 4: Let debug builds find the tools**

In `src/YtAudioDownloader/YtAudioDownloader.csproj`, add before `</Project>`:

```xml
  <!-- For running from the IDE: scripts\fetch-tools.ps1 -Destination .tools-cache\dev-tools -->
  <ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)..\..\.tools-cache\dev-tools')">
    <None Include="$(MSBuildThisFileDirectory)..\..\.tools-cache\dev-tools\**\*"
          Link="tools\%(RecursiveDir)%(Filename)%(Extension)"
          CopyToOutputDirectory="PreserveNewest"
          CopyToPublishDirectory="Never" />
  </ItemGroup>
```

- [ ] **Step 5: Replace `.github/workflows/build.yml`**

```yaml
name: Build

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    if: github.ref_type != 'tag'
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - name: Test
        run: dotnet test YtAudioDownloader.sln -c Release

  release:
    if: github.ref_type == 'tag'
    runs-on: windows-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - name: Build release zip
        shell: pwsh
        run: |
          $version = $env:GITHUB_REF_NAME.TrimStart('v')
          ./build.ps1 -Version $version
      - name: Publish GitHub Release
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
        run: gh release create $env:GITHUB_REF_NAME publish/YtAudioDownloader.zip --title "YouTube Audio Downloader $env:GITHUB_REF_NAME" --notes-file docs/release-notes.md
```

- [ ] **Step 6: Create `docs/user-readme.txt`** (copied into the zip as `README.txt`)

```text
YouTube Audio Downloader
========================

RU
--
1. Запустите YtAudioDownloader.exe. При первом запуске Windows может показать синее окно
   «Windows защитила ваш компьютер» — нажмите «Подробнее», затем «Выполнить в любом случае».
2. Вставьте ссылку на YouTube. Удобно: на YouTube нажмите «Поделиться», отметьте «Начало»
   и скопируйте ссылку — время начала заполнится само, конец будет через 30 секунд.
3. Нажмите «Скачать». Файл появится в папке «Музыка\YtAudioDownloader».
Не удаляйте папку tools — в ней программы, без которых скачивание не работает.
Если что-то пошло не так, нажмите «Скопировать подробности» и отправьте текст тому,
кто дал вам эту программу.

EN
--
1. Run YtAudioDownloader.exe. On first start Windows may show a blue "Windows protected your PC"
   screen: click "More info", then "Run anyway".
2. Paste a YouTube link. Tip: on YouTube click Share, tick "Start at" and copy the link; the start
   time fills in by itself and the end is set 30 seconds later.
3. Click Download. The file appears in Music\YtAudioDownloader.
Keep the tools folder next to the program: it holds the helpers the download needs.
If something goes wrong, click "Copy details" and send the text to whoever gave you this app.
```

- [ ] **Step 7: Create `docs/release-notes.md`**

```markdown
**Download:** `YtAudioDownloader.zip` below. Unpack the whole archive anywhere and run `YtAudioDownloader\YtAudioDownloader.exe`. Nothing else to install.

**Скачать:** `YtAudioDownloader.zip` ниже. Распакуйте архив целиком в любую папку и запустите `YtAudioDownloader\YtAudioDownloader.exe`. Больше ничего устанавливать не нужно.

### First start / Первый запуск
Windows may show "Windows protected your PC" because the app is not signed: click **More info → Run anyway** (only once).
Windows может показать «Windows защитила ваш компьютер», потому что программа не подписана: нажмите **Подробнее → Выполнить в любом случае** (только один раз).

### Quick check / Быстрая проверка
1. Start the app. Within a minute the bottom line shows `yt-dlp <version>`. / Запустите программу. Через минуту внизу появится `yt-dlp <версия>`.
2. On YouTube: Share → tick "Start at" → copy. Paste the link: Start fills in, End is 30 s later. / На YouTube: «Поделиться» → «Начало» → скопировать. Вставьте ссылку: начало заполнится, конец — через 30 с.
3. Click Download: the progress bar moves and the file appears in `Music\YtAudioDownloader`. / Нажмите «Скачать»: полоса прогресса движется, файл появляется в `Музыка\YtAudioDownloader`.
4. Play it: it starts and ends with a short fade. / Включите файл: в начале и в конце — плавный переход.
5. Any error: click "Copy details" and send the text. / Если ошибка — нажмите «Скопировать подробности» и отправьте текст.

The app updates its download engine (yt-dlp) by itself on every start. / Программа сама обновляет yt-dlp при каждом запуске.
```

- [ ] **Step 8: Replace `README.md`**

````markdown
# YouTube Audio Downloader

A small Windows app: paste a YouTube link, choose start and end, get an MP3 of that part
(with a short fade in/out and even volume). Made for one friend; works out of the box on Windows 10/11.

## For users

Download [`YtAudioDownloader.zip`](https://github.com/knilsyansky/robert-audio/releases/latest/download/YtAudioDownloader.zip)
from the latest release, unpack it, run `YtAudioDownloader.exe`. On first start Windows SmartScreen may say
"Windows protected your PC": click **More info → Run anyway**. See the release notes for a short checklist.

- Times: `95`, `1:35`, `0:01:35`, `1:35.5` all work. A link with `&t=95s` (YouTube "Share → Start at") fills Start in.
- Files go to `Music\YtAudioDownloader` as `<video title> (1m35s-2m05s).mp3`; existing files are never overwritten.
- The app keeps its own copy of yt-dlp in `%LOCALAPPDATA%\YtAudioDownloader` and updates it (nightly channel) on every start.
  If YouTube blocks a download, it updates and retries once. Logs: `%LOCALAPPDATA%\YtAudioDownloader\logs`.

## For developers

Needs the .NET 10 SDK.

```powershell
dotnet test YtAudioDownloader.sln                                   # unit tests
.\scripts\fetch-tools.ps1 -Destination .tools-cache\dev-tools       # once, so F5/dotnet run finds yt-dlp, deno, ffmpeg
dotnet run --project src\YtAudioDownloader                          # run the app
.\build.ps1 -Version 1.2.3                                          # publish\YtAudioDownloader.zip (or build.bat)
```

Headless smoke test of a build: `YtAudioDownloader.exe --clip <url> <start> <end> <output folder>` prints `OK <file>` or `ERROR ...`.
Environment overrides: `YTAUDIO_DATA_DIR` (data folder), `YTAUDIO_LANG=ru|en` (UI language).

Layout: `src/YtAudioDownloader` (app), `tests/YtAudioDownloader.Tests` (xUnit), `scripts/` (tool download, screenshots).

## Releasing

Push a tag: `git tag v1.0.0 && git push origin v1.0.0`. GitHub Actions runs `build.ps1`, then publishes a release with
`YtAudioDownloader.zip` (app + yt-dlp nightly + deno + ffmpeg) and `docs/release-notes.md` as the description.
````

- [ ] **Step 9: Build the release zip locally**

Run: `.\build.ps1 -Version 1.0.0`
Expected: tests pass, publish succeeds, tools download, ends with `Done: ...\publish\YtAudioDownloader.zip`. The zip contains `YtAudioDownloader\YtAudioDownloader.exe`, `YtAudioDownloader\README.txt`, `YtAudioDownloader\tools\{yt-dlp.exe, deno.exe, ffmpeg.exe, licenses\...}`. Report the zip size.

- [ ] **Step 10: End-to-end check of the packaged app on a "clean" setup**

Unpack the zip into a scratch folder outside the repo, point the data folder at an empty directory and strip PATH so node and any system ffmpeg are invisible. In one PowerShell invocation (`<scratch>` = a temp folder without spaces):

```powershell
$e2e = '<scratch>\e2e'
if (Test-Path $e2e) { Remove-Item $e2e -Recurse -Force }
Expand-Archive publish\YtAudioDownloader.zip -DestinationPath $e2e
$env:YTAUDIO_DATA_DIR = "$e2e\data"
$env:PATH = 'C:\Windows\System32;C:\Windows'
$exe = "$e2e\YtAudioDownloader\YtAudioDownloader.exe"

$p = Start-Process $exe -ArgumentList '--clip', 'https://www.youtube.com/watch?v=Ex-dEn5KAsY', '1:00', '1:30', "$e2e\out" -Wait -PassThru -NoNewWindow -RedirectStandardOutput "$e2e\run1.txt"
"exit=$($p.ExitCode)"; Get-Content "$e2e\run1.txt" -Encoding UTF8

$p = Start-Process $exe -ArgumentList '--clip', 'https://www.youtube.com/watch?v=jNQXAC9IVRw', '0:05', '0:40', "$e2e\out" -Wait -PassThru -NoNewWindow -RedirectStandardOutput "$e2e\run2.txt"
"exit=$($p.ExitCode)"; Get-Content "$e2e\run2.txt" -Encoding UTF8

Get-ChildItem "$e2e\out" | Select-Object Name, Length
Get-ChildItem "$e2e\out\*.mp3" | ForEach-Object { & "$e2e\YtAudioDownloader\tools\ffmpeg.exe" -hide_banner -i $_.FullName 2>&1 | Select-String 'Duration' }
Test-Path "$e2e\data\yt-dlp.exe"
Select-String -Path "$e2e\data\logs\*.log" -Pattern 'js-runtimes deno:' -List | Select-Object -First 1
Select-String -Path "$e2e\data\logs\*.log" -Pattern 'No supported JavaScript runtime'
```

Expected:
- Run 1: `exit=0`, a `yt-dlp <version> (update ok)` line, then `OK ...\out\Crypt of The Seetherdancer (1m00s-1m30s).mp3`; ffmpeg reports `Duration: 00:00:30.0x` (a few ms over 30 s is normal for MP3).
- Run 2 (a 19-second video, end past the video): `exit=0`, file name ends with the clamped end reported by yt-dlp (about `(0m05s-0m19s).mp3`), duration ≈ 14 s.
- `True` for the copied `data\yt-dlp.exe`; the log contains `js-runtimes deno:`; the last `Select-String` prints nothing (deno was used, no JS runtime warning).

If anything differs, fix the cause (not the expectation) and re-run Steps 9–10. Record the actual outputs in the report.

- [ ] **Step 11: Screenshot the packaged app in both languages**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/screenshot.ps1 -Exe <scratch>\e2e\YtAudioDownloader\YtAudioDownloader.exe -Lang ru -Out <scratch>\packaged-ru.png
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/screenshot.ps1 -Exe <scratch>\e2e\YtAudioDownloader\YtAudioDownloader.exe -Lang en -Out <scratch>\packaged-en.png
```

(Without `YTAUDIO_DATA_DIR` set, this uses the real `%LOCALAPPDATA%\YtAudioDownloader`; that is intended — it is the path the friend's machine will use.)
Expected: the window title shows `1.0.0`; the bottom line shows a yt-dlp version (either `yt-dlp checking...` if captured early, or `yt-dlp <version>`), not "not found". Look at both PNGs and describe them in the report.

- [ ] **Step 12: Commit**

```powershell
git add scripts/fetch-tools.ps1 build.ps1 build.bat docs/release-notes.md docs/user-readme.txt README.md .github/workflows/build.yml src/YtAudioDownloader/YtAudioDownloader.csproj
git commit -m "Add tool download, release build, GitHub release workflow and docs" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
