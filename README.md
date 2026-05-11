# FlaUI-MCP

An MCP (Model Context Protocol) server that enables AI agents to automate Windows desktop applications using accessibility APIs - the same way Playwright automates browsers.

[![CI](https://github.com/starpia-forge/FlaUI-MCP/actions/workflows/build.yml/badge.svg)](https://github.com/starpia-forge/FlaUI-MCP/actions/workflows/build.yml)
[![CD](https://github.com/starpia-forge/FlaUI-MCP/actions/workflows/release.yml/badge.svg)](https://github.com/starpia-forge/FlaUI-MCP/actions/workflows/release.yml)
[![GitHub release](https://img.shields.io/github/v/release/starpia-forge/FlaUI-MCP)](https://github.com/starpia-forge/FlaUI-MCP/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> **Fork notice** — This repository is a fork of [shanselman/FlaUI-MCP](https://github.com/shanselman/FlaUI-MCP). It diverges from upstream with stability fixes (HWND-based window detection for UWP and localized titles, auto-wait/retry executor, handle/GDI leak fixes, JSON-RPC 2.0 / MCP spec compliance), a multi-arch CI/CD pipeline (win-x64 + win-arm64) driven by semantic-release, and an xUnit test suite. See [Changes from upstream](#changes-from-upstream) for details.

## Why This Exists

When Playwright's MCP server automates browsers, it provides:
- `browser_snapshot` → Structured accessibility tree with element refs
- `browser_click ref="..."` → Click by ref, not coordinates

**FlaUI-MCP brings the same pattern to Windows desktop apps:**
- `windows_snapshot` → Accessibility tree with refs like `w1e5`
- `windows_click ref="w1e5"` → Click element by ref

No screenshot parsing. No coordinate guessing. Just semantic element references.

## Quick Demo

```
Agent: Calculate 3 × 3

1. windows_launch { "app": "calc.exe" }
   → Window handle: w1

2. windows_snapshot { "handle": "w1" }
   → - window "Calculator" [ref=w1]
       - button "Three" [ref=w1e43]
       - button "Multiply by" [ref=w1e35]
       - button "Equals" [ref=w1e38]
       - text "Display is 0" [ref=w1e15]

3. windows_batch { "actions": [
     {"action": "click", "ref": "w1e43"},
     {"action": "click", "ref": "w1e35"},
     {"action": "click", "ref": "w1e43"},
     {"action": "click", "ref": "w1e38"},
     {"action": "snapshot", "handle": "w1"}
   ]}
   → 1. click: Invoked Three
     2. click: Invoked Multiply by
     3. click: Invoked Three
     4. click: Invoked Equals
     5. snapshot: ... "Display is 9" ...
```

## Installation

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime

### Download Release

Grab the latest build from [Releases](https://github.com/starpia-forge/FlaUI-MCP/releases). Four artifacts are published per release:

| Artifact | When to choose |
|---|---|
| `FlaUI-MCP-win-x64-<v>-self-contained.zip` | Intel/AMD 64-bit Windows, no .NET install required |
| `FlaUI-MCP-win-x64-<v>-framework-dependent.zip` | Intel/AMD 64-bit Windows, .NET 8 runtime already installed |
| `FlaUI-MCP-win-arm64-<v>-self-contained.zip` | ARM64 Windows (Surface Pro X, Copilot+ PCs), no .NET install required |
| `FlaUI-MCP-win-arm64-<v>-framework-dependent.zip` | ARM64 Windows, .NET 8 runtime already installed |

Extract the ZIP to any folder; the executable is `FlaUI.Mcp.exe`.

### Configure MCP Client

Add to your MCP configuration (e.g., `~/.copilot/mcp-config.json`):

```json
{
  "mcpServers": {
    "windows": {
      "type": "local",
      "command": "C:\\path\\to\\FlaUI-MCP.exe",
      "tools": ["*"]
    }
  }
}
```

Or using `dotnet run`:

```json
{
  "mcpServers": {
    "windows": {
      "type": "local",
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\src\\FlaUI.Mcp"]
    }
  }
}
```

## Available Tools

| Tool | Description |
|------|-------------|
| `windows_launch` | Launch a Windows application |
| `windows_snapshot` | Get accessibility tree with element refs |
| `windows_click` | Click an element by ref |
| `windows_type` | Type text into an element |
| `windows_fill` | Clear and fill a text field |
| `windows_get_text` | Get text content of an element |
| `windows_screenshot` | Capture window/element as PNG |
| `windows_list_windows` | List all open windows |
| `windows_focus` | Bring a window to foreground |
| `windows_close` | Close a window |
| `windows_batch` | Execute multiple actions in one call |

## How It Works

### The Accessibility Snapshot

When you call `windows_snapshot`, you get a structured text tree:

```
- window "Calculator" [ref=w1e1]
  - group "Number pad" [ref=w1e39]
    - button "Seven" [ref=w1e47]
    - button "Eight" [ref=w1e48]
    - button "Nine" [ref=w1e49]
  - text "Display is 0" [ref=w1e15]
```

This comes from **Windows UI Automation** - the same API screen readers use. Each element has:
- **Role** (button, text, group, textbox)
- **Name** ("Seven", "Display is 0")
- **Ref** (w1e47) - a handle for interaction
- **State** ([disabled], [readonly], [checked])

### Why Not Screenshots?

| Approach | Pros | Cons |
|----------|------|------|
| **Accessibility Tree** | Semantic, precise, fast, works at any resolution | Requires UI Automation support |
| **Screenshot + Vision** | Works with any app | Slow, expensive, imprecise, resolution-dependent |

FlaUI-MCP uses accessibility because it's what screen readers use - it's designed for programmatic UI interaction.

## Building from Source

```powershell
# Clone
git clone https://github.com/starpia-forge/FlaUI-MCP.git
cd FlaUI-MCP

# Build
dotnet build FlaUI.Mcp.sln

# Run
dotnet run --project src/FlaUI.Mcp
```

## Testing

```powershell
dotnet test FlaUI.Mcp.sln
```

Unit tests live under `tests/FlaUI.Mcp.Tests/` (xUnit) and cover `ElementRegistry` and `SnapshotBuilder` helpers. The CI workflow runs them automatically for the `win-x64` matrix leg.

## Changes from upstream

This fork diverges from [shanselman/FlaUI-MCP](https://github.com/shanselman/FlaUI-MCP) in four areas:

**Stability & correctness**
- HWND-diff window detection — `windows_launch` now identifies the new window by comparing top-level HWNDs before and after process start, instead of by title substring. This is required for UWP apps (Calculator launches via `ApplicationFrameHost.exe`) and for non-English Windows where the localized title (e.g. `계산기`) never matches the English filename.
- Auto-wait & stale-element retry — `ActionExecutor` re-resolves stored locators when UI Automation throws `ElementNotAvailable` / `NoClickablePoint` / `UIA_E_ELEMENTNOTAVAILABLE`, with a configurable timeout (default 5 s).
- JSON-RPC 2.0 / MCP spec compliance fixes (notification handling, error envelopes).
- Handle leak, GDI leak, and thread-safety fixes around session and window registration.

**Build & release pipeline**
- `CI` (`.github/workflows/build.yml`) — runs on every branch push and PR. Matrix build for `win-x64` + `win-arm64`, plus xUnit tests on the x64 leg.
- `CD` (`.github/workflows/release.yml`) — runs on `main` only. Uses [semantic-release](https://semantic-release.gitbook.io/) (`.releaserc.json`) to read Conventional Commits, decide the next version, tag it, and publish a GitHub release with all four ZIPs attached.
- `GitVersion.yml` supplies SemVer-compatible assembly versions to in-progress builds.

**Project layout**
- Solution file (`FlaUI.Mcp.sln`) added; source folder renamed from `src/PlaywrightWindows.Mcp` to `src/FlaUI.Mcp`; namespace renamed from `PlaywrightWindows.Mcp` to `FlaUI.Mcp`.

**Tests**
- `tests/FlaUI.Mcp.Tests/` added with xUnit coverage for `ElementRegistry` and `SnapshotBuilder` helpers.

### Contributing to this fork

Commits on `main` must follow [Conventional Commits](https://www.conventionalcommits.org/) — `feat:` triggers a minor release, `fix:` triggers a patch release, and `BREAKING CHANGE:` in the body triggers a major release. Anything else (`chore:`, `docs:`, `ci:`, `refactor:`, `test:`) is shipped silently with no release.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  AI Agent (GitHub Copilot, Claude, etc.)                        │
│  - Calls MCP tools: windows_snapshot, windows_click, etc.       │
└─────────────────────────────────────────────────────────────────┘
                              │ MCP Protocol (JSON-RPC over stdio)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  FlaUI-MCP Server (.NET 8)                                      │
│  - Implements MCP tool handlers                                 │
│  - Builds agent-friendly accessibility snapshots                │
│  - Maps element refs ↔ AutomationElements                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  FlaUI Library (github.com/FlaUI/FlaUI)                         │
│  - UIA3Automation for modern apps (WPF, UWP, Win32)            │
│  - Control patterns: Invoke, Value, Toggle, Selection           │
│  - Tree walking and element discovery                           │
└─────────────────────────────────────────────────────────────────┘
```

## Supported Applications

Works with any Windows application that supports UI Automation:
- ✅ Win32 apps (Notepad, Explorer, etc.)
- ✅ WPF applications
- ✅ WinForms applications  
- ✅ UWP/Store apps (Calculator, Settings, etc.)
- ⚠️ Electron apps (partial - depends on accessibility implementation)
- ❌ Games (typically no UI Automation support)

## Contributing

Contributions welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

- [shanselman/FlaUI-MCP](https://github.com/shanselman/FlaUI-MCP) — The upstream project this fork is built on
- [FlaUI](https://github.com/FlaUI/FlaUI) - The excellent .NET UI Automation library this project is built on
- [Playwright](https://playwright.dev/) - Inspiration for the snapshot/ref interaction model
- [Model Context Protocol](https://modelcontextprotocol.io/) - The protocol that makes this possible
