# YouTube Audio Downloader rework — design

Agreed with the owner (Roman) on 2026-09-13 through a grilling session. This is the binding spec for
`docs/superpowers/plans/2026-09-13-yt-audio-rework.md`.

## Purpose

A Windows 10/11 app for one non-technical user (a friend of the owner). The friend pastes a YouTube link,
sets a start and end time (typically ~30 s out of a ~3 min video), and gets an MP3 of that part.
It must work "out of the box": unzip, run, nothing else to install.

## Diagnosis of the reported bug

The friend saw `ERROR: unable to download video data: HTTP Error 403: Forbidden`.
Reproduced on the owner's PC on 2026-09-13:

| yt-dlp | JS runtime | result |
|---|---|---|
| 2026.03.17 (bundled) | none | HTTP 403 |
| 2026.03.17 | node | HTTP 403 |
| 2026.08.19 (latest) | none | OK |
| 2026.08.19 | node | OK |

Root cause: the bundled yt-dlp was 6 months old; YouTube changed and old versions pick a client whose stream
URLs return 403. The app had no way to keep yt-dlp current. The "no JavaScript runtime" warning is not the cause
today, but yt-dlp calls running without one deprecated, so a runtime (deno) is bundled for the future.

## Decisions

### Keeping yt-dlp working
- The app keeps its own copy of yt-dlp in `%LOCALAPPDATA%\YtAudioDownloader\`. The copy bundled in the zip is
  only the seed: copied there on first run (or when the local copy is missing/broken).
- Every app start, in the background, the local copy is updated with `yt-dlp --update-to nightly`.
  Offline/failed update: keep the existing copy silently and show a small status line
  (`yt-dlp <version> · update check failed`). No popup.
- `deno.exe` ships in the zip; the app passes it to yt-dlp with `--js-runtimes deno:<path>`.
- If a download fails because YouTube blocked it (403 / 429 / "not a bot") or for an unknown reason, the app
  updates yt-dlp and retries once before showing an error.
- Errors are short human messages in the user's language with a "Copy details" button (full technical report to
  the clipboard) and a log file in `%LOCALAPPDATA%\YtAudioDownloader\logs\`.
- No manual "Update" button, no "Use browser cookies" checkbox.

### The app
- C# WinForms, self-contained single-file build, .NET 10 (8 goes out of support in November 2026).
- Keep the pipeline: download the whole audio track with yt-dlp, then cut locally with ffmpeg.
- Time inputs accept `95`, `1:35`, `0:01:35`, `1:35.5` (and `1:35,5`) the way a person means them.
- If the pasted URL has a timestamp (`&t=95s`, from YouTube's "Share → Start at"), Start is filled in
  automatically. End defaults to Start + 30 s until the user edits End.
- Fade in/out: checkbox (on by default) plus a seconds box (default 3 s); fades shrink automatically for short clips.
- Normalize volume: checkbox, on by default (ffmpeg `loudnorm`).
- Output: folder `Music\YtAudioDownloader` by default, the last chosen folder is remembered; file name
  `<video title> (1m35s-2m05s).mp3` with characters Windows forbids removed; never overwrite (add ` (2)`, ` (3)`).
  MP3 only, same quality as before (libmp3lame VBR `-q:a 2`).
- Progress bar, Cancel button, "Open folder" button after success.
- UI language: Russian if Windows is Russian, otherwise English.
- Latent bugs fixed along the way: arguments passed safely (no string concatenation), yt-dlp output read as UTF-8
  (Cyrillic titles), stdout/stderr read concurrently (no deadlock), numbers passed to ffmpeg culture-invariant
  (Russian Windows uses a decimal comma).

### Repo and release
- Commits as `knilsyansky` (repo-local config, already set). Work on branch `rework`, open a PR.
- The old uncommitted WIP (cookies checkbox + update button) is stashed, not used.
- `ffmpeg.exe` and `yt-dlp.exe` are no longer tracked in git (history untouched). `installer.iss` is deleted.
  `build.bat` downloads tools itself.
- CI: every push/PR builds and runs tests. A pushed `v*` tag builds `YtAudioDownloader.zip` (app + yt-dlp nightly +
  deno + pinned ffmpeg + license notes) and publishes a GitHub Release. Stable link:
  `https://github.com/knilsyansky/robert-audio/releases/latest/download/YtAudioDownloader.zip`.
- No in-app "new version available" notice.
- SmartScreen warning accepted; README and release notes explain "More info → Run anyway" and include a
  5-line test checklist for the friend.

### Verification
- Unit tests for time parsing, `&t=` extraction, file naming, fade length, command building and output parsing,
  and the download/retry orchestration; run in CI.
- Build the release zip locally and run it with an empty data folder and nothing on PATH (no node, no system
  ffmpeg); do real clip downloads through the packaged app.
- A clean Windows 10 test is not possible here; the friend's first run of v1.0.0 is the real test.
