# Installer build

This project uses **Inno Setup 6** to create a Windows installer.

## Prerequisites

- .NET SDK 8
- Inno Setup 6 (`ISCC.exe`)

## Build installer

Run from repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

One-click wrappers (run from `installer` folder):

```powershell
.\build-installer-wrapper.ps1 -Profile all
.\build-installer-wrapper.ps1 -Profile x64
.\build-installer-wrapper.ps1 -Profile x86
```

```cmd
build-installer-wrapper.cmd all
build-installer-wrapper.cmd x64
build-installer-wrapper.cmd x86
```

Shortcut CMD wrappers (no profile argument needed):

```cmd
build-all.cmd
build-x64.cmd
build-x86.cmd
```

Result:

- `installer/output/Priority Manager X-Setup-x64.exe`
- `installer/output/Priority Manager X-Setup-x86.exe`

## Notes

- The script publishes self-contained single-file app binaries for both `win-x64` and `win-x86` by default.
- To build only one installer, pass runtimes explicitly, for example:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Runtimes win-x86
```

## CI / Release commands

Build both installers (default):

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Build x64 only:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Runtimes win-x64
```

Build x86 only:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Runtimes win-x86
```

Build both explicitly (useful for CI templates):

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Runtimes win-x64,win-x86
```

- Installer languages: English, Russian, Ukrainian.
- Installer UI language is selected automatically based on Windows language when available.
