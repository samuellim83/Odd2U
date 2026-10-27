# Packaging

The public repository contains source and packaging instructions only. The AI model, runtime, installer compiler and generated installers are excluded from Git.

## Offline package staging

Build the app with `./build.ps1 -OutputDirectory dist/Odd2U-Portable` in PowerShell. Prepare this structure:

```
dist/Odd2U-Portable/
  Odd2U.exe
  odd-personality.txt
  portable.flag
  START-HERE.txt
  licenses/
  ai/
    ollama.exe
    lib/ollama/          # CPU runtime DLLs, runner and upstream notices
    models/
      manifests/registry.ollama.ai/library/qwen3/1.7b
      blobs/            # All config/layer blobs referenced by the manifest
```

The tested bundle used Ollama 0.40.7 and Qwen3 1.7B. Obtain binaries from the official Ollama distribution and model through `ollama pull qwen3:1.7b`. Include upstream license notices and verify copied model digests against its manifest. CPU-only packaging omits GPU runtime directories but retains the top-level CPU libraries and runner. Future runtime versions may have different requirements; test them before distribution.

Never copy chats, settings, account keys, logs or a user's complete Ollama directory into a release. `portable.flag` is an empty/text marker; portable runtime data is created in `Data/` on use. Exclude Data from release archives.

Compress the clean staging directory into a ZIP for portable use. Test extraction and an AI request from the extracted copy. `BundleCheck.cs` provides a local model smoke test; do not distribute its compiled executable or test data.

## Installer

Use an official Inno Setup 6 compiler (tested: 6.7.3), with licensing appropriate to your use:

```powershell
& 'C:\path\to\Inno Setup 6\ISCC.exe' packaging\Odd2U.iss
```

The definition reads `dist/Odd2U-Portable`, excludes its portable marker and data, and writes a Windows 11 x64 per-user installer to `dist/`. It adds Start menu entries, offers an optional desktop shortcut, and installs no service or startup task. Uninstall preserves user data in `%LOCALAPPDATA%\Odd2U`.

Test installation and removal on a clean PC before wider sharing. Sign production releases and include checksums. No release binaries or model files are uploaded with this source repository.
