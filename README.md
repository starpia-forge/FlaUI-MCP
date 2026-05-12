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
- .NET 8.0 Runtime (for the release binaries) **or** .NET 8.0 SDK (for the Claude Code plugin install and building from source)

### Install as a Claude Code plugin (recommended for Claude Code users)

If you use [Claude Code](https://code.claude.com/), install FlaUI-MCP as a plugin with two commands — no manual MCP config file editing required:

```bash
/plugin marketplace add starpia-forge/FlaUI-MCP
/plugin install flaui-mcp@flaui-mcp-marketplace
```

Claude Code spawns the MCP server automatically. The 26 `windows_*` tools become available in your session immediately.

**Prerequisite**: .NET 8.0 SDK on `PATH` (the plugin invokes `dotnet run`; the SDK builds the server from source on first launch). If you only have the .NET runtime, use the release-binary install below instead.

> First launch takes ~10–30 s for the initial restore + build. Subsequent launches reuse the cached build (~1–3 s).
>
> Update with `/plugin update flaui-mcp@flaui-mcp-marketplace`; uninstall with `/plugin uninstall flaui-mcp@flaui-mcp-marketplace`.

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

### Session

| Tool | Description |
|------|-------------|
| `windows_launch` | Launch a Windows application |
| `windows_attach` | Attach to a running process by PID or executable name; returns handles for every UIA-visible window (including hidden ones typical of tray-resident apps) |

### Window

| Tool | Description |
|------|-------------|
| `windows_list_windows` | List all open windows; pass `includeHidden=true` to surface windows with empty titles (tray-resident apps) |
| `windows_focus` | Bring a window to foreground |
| `windows_close` | Close a window |
| `windows_window_state` | Maximize, minimize, or restore a window via WindowPattern; move or resize via TransformPattern |

### Inspect

| Tool | Description |
|------|-------------|
| `windows_snapshot` | Get accessibility tree with element refs (also accepts popup handles `m1`, `m2`, … from `windows_context_menu` or `windows_tray_invoke`). Pass `verbose:true` to include AutomationId and BoundingRect per element. |
| `windows_inspect` | Dump all UIA properties (AutomationId, ClassName, BoundingRect, …) and supported patterns with current state for one element ref |
| `windows_get_text` | Get text content of an element |
| `windows_screenshot` | Capture window/element as PNG |

### Values

| Tool | Description |
|------|-------------|
| `windows_get_value` | Read an element's current value via UIA patterns (Value → RangeValue → Toggle → SelectionItem); use instead of `windows_get_text` for sliders, checkboxes, and combo boxes |
| `windows_set_value` | Set an element's value — string → Value/SelectionItem pattern, number → RangeValue (slider), boolean → Toggle (checkbox) |

### Mouse

| Tool | Description |
|------|-------------|
| `windows_click` | Click an element by ref |
| `windows_hover` | Move mouse over an element to trigger hover-only UI |
| `windows_scroll` | Scroll within an element (UIA ScrollPattern or mouse wheel) |
| `windows_drag` | Drag from one element to another or to absolute coordinates |

### Keyboard

| Tool | Description |
|------|-------------|
| `windows_type` | Type text into an element |
| `windows_fill` | Clear and fill a text field |
| `windows_keys` | Send keyboard shortcuts or sequences (`Ctrl+S`, `Alt+F4`, `Tab`) |

### Tray & Menu

| Tool | Description |
|------|-------------|
| `windows_tray_list` | Enumerate Windows notification-area (system tray) icons; returns refs usable with `windows_tray_invoke` |
| `windows_tray_invoke` | Click a tray icon by ref (left/right/middle, single or double); right-click auto-registers the context menu as a popup handle |
| `windows_context_menu` | Right-click an element (or send Shift+F10 / VK_APPS) and register the resulting context menu as a popup handle for `windows_snapshot` |
| `windows_dismiss_menu` | Send Escape to close an open context menu and remove its popup handle from the registry |

### Flow

| Tool | Description |
|------|-------------|
| `windows_batch` | Execute multiple actions in one call |
| `windows_wait_for` | Poll until a condition holds (visible, enabled, textContains, valueEquals, expanded, focused, selectionContains, …) |
| `windows_assert` | One-shot structured PASS/FAIL condition check |

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

### Tray-Resident Apps (Discord, Slack, etc.)

Apps that "minimize to tray" have no titled top-level window, so `windows_list_windows` and `windows_focus` can't find them. Use `windows_tray_list` to enumerate the notification-area icons, then `windows_tray_invoke` to click the owner — its window gets auto-registered and returned as a handle. Alternatively, attach by process name with `windows_attach { "processName": "Discord" }` and pass `includeHidden=true` to `windows_list_windows` to see hidden windows.

> **Limitation:** requires the classic Explorer taskbar (Win10, or Win11 with classic taskbar). The Win11 native taskbar (22H2+) hides `Shell_TrayWnd` from UIA and is not yet supported.

### Context Menus

Right-click context menus (Win32 class `#32768`) are transient — they dismiss on focus loss. The workflow is:

```
1. windows_context_menu { "ref": "w1e5" }
   → Popup registered: m1

2. windows_snapshot { "handle": "m1" }
   → - menu [ref=m1e1]
       - menuitem "Cut" [ref=m1e2]
       - menuitem "Copy" [ref=m1e3]
       - menuitem "Paste" [ref=m1e4]

3. windows_click { "ref": "m1e3" }
   → Invoked Copy

4. windows_dismiss_menu { "handle": "m1" }   ← optional; menu auto-closes after click
```

For tray-icon right-click menus, use `windows_tray_invoke` with `button: "right"` — it performs the same discovery and returns a popup handle directly. Do **not** call `windows_list_windows` or `windows_focus` between steps 1 and 3; any window-enumeration call can dismiss the menu before the click lands.

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

Unit tests live under `tests/FlaUI.Mcp.Tests/` (xUnit) and cover `ElementRegistry`, `SnapshotBuilder`, `ConditionEvaluator`, and `KeyMap` helpers. The CI workflow runs them automatically for the `win-x64` matrix leg.

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
