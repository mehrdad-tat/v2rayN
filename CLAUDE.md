# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

v2rayN is a cross-platform GUI client for Windows, Linux, and macOS supporting Xray, sing-box, and other VPN/proxy cores. Built with .NET 10.0 using WPF for Windows and Avalonia for cross-platform UI.

## Build Commands

```bash
# Build Windows WPF version (from v2rayN/ directory)
dotnet build ./v2rayN/v2rayN.csproj -c Debug

# Build cross-platform Avalonia version
dotnet build ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Debug

# Build entire solution
dotnet build ./v2rayN.sln -c Debug

# Publish for specific platforms
dotnet publish ./v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 -p:SelfContained=true
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r osx-x64 -p:SelfContained=true
```

Note: Windows WPF builds on Linux require `-p:EnableWindowsTargeting=true`.

## Solution Structure

```
v2rayN/
├── ServiceLib/         # Core business logic (shared across all platforms)
├── v2rayN/             # Windows WPF application
├── v2rayN.Desktop/     # Avalonia cross-platform application
├── AmazTool/           # Upgrade utility console app
└── GlobalHotKeys/      # Git submodule for hotkey handling
```

## Architecture

**MVVM Pattern with ReactiveUI**: All UI applications use Model-View-ViewModel architecture with reactive bindings.

**ServiceLib** is the core library containing:
- `Manager/` - Singletons managing app state (AppManager), cores (CoreManager), statistics, etc.
- `Services/` - Core config generation (V2ray, Singbox, Clash), downloads, updates, speedtests
- `Models/` - Data models (ProfileItem, Config, routing, DNS)
- `Handler/` - System proxy handling, format converters (Fmt*)
- `Enums/` - ECoreType, EConfigType, ETransport, EInboundProtocol, etc.

**AppManager** (`ServiceLib/Manager/AppManager.cs`) is the central singleton:
- Holds application `Config`
- Manages SQLite database tables (SubItem, ProfileItem, RoutingItem, DNSItem, etc.)
- Determines core types and port assignments

**Core Config Services** generate JSON configs for different cores:
- `Services/CoreConfig/V2ray/` - V2ray/Xray config generation
- `Services/CoreConfig/Singbox/` - sing-box config generation
- `Services/CoreConfig/CoreConfigClashService.cs` - Clash config

## Code Style (from .editorconfig)

Key rules enforced:
- File-scoped namespaces required (`namespace X;`)
- Use `var` when type is apparent
- Prefer pattern matching over `as`/`is` with casts
- Always include accessibility modifiers
- Use null propagation (`?.`) and null coalescing (`??`)
- Braces required for all control flow
- Avoid collection initializers (use explicit construction)
- Avoid conditional operators for assignment (prefer if/else)
- Avoid switch expressions (use traditional switch)
- PascalCase for types, methods, properties; interfaces with "I" prefix

## Key Technologies

- **UI**: WPF (Windows), Avalonia 11.x (cross-platform)
- **Reactive**: ReactiveUI 22.x for MVVM bindings
- **Database**: SQLite via sqlite-net-pcl
- **Logging**: NLog
- **Serialization**: YamlDotNet, System.Text.Json
- **HTTP**: Downloader library, WebDav.Client
- **Process Management**: CliWrap

## Supported VPN Cores

Configured via `ECoreType` enum:
- Xray, v2fly, v2fly_v5
- sing_box, mihomo (Clash)
- Hysteria2, tuic, juicity
- clash, clash_meta

## Localization

Resources in `ServiceLib/Resx/` with support for:
- English (default)
- Chinese Simplified/Traditional
- Russian, Persian, French, Hungarian

## Git Submodules

Initialize with `git submodule update --init --recursive` for GlobalHotKeys library.
