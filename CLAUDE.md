# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UsbBridge is a WinUI 3 desktop application (.NET 10.0, Windows-only, unpackaged) that automatically forwards USB devices from Windows to WSL via `usbipd`. It runs in the system tray, monitors connected USB devices, and attaches them to WSL based on user-defined glob pattern rules.

## Build & Run Commands

```shell
dotnet build           # Build the project (x64)
dotnet run             # Build and run
dotnet publish         # Publish self-contained (win-x64, folder output)
```

No test framework is configured. No Visual Studio required — builds from `dotnet` CLI only.

## Architecture

MVVM pattern, no DI container — `MainViewModel` is instantiated via XAML `DataContext` on the root Grid.

- **View**: `MainWindow.xaml` — WinUI 3 with Mica backdrop, custom title bar (`ExtendsContentIntoTitleBar`), `TabView`, `ItemsRepeater`, dark theme via `RequestedTheme="Dark"`. `App.xaml.cs` sets up the system tray icon via `H.NotifyIcon.WinUI` with `ContextFlyout` (MenuFlyout + MenuFlyoutItem).
- **ViewModel**: `MainViewModel.cs` contains three `ObservableObject` classes:
  - `MainViewModel` — app state, commands (`AddRule`, `RemoveRule`, `ForwardDevice`), service orchestration, `DispatcherQueue`-based dispatch
  - `DeviceViewModel` — per-device display state (busId, vidPid, description, status, statusColor, hasRule)
  - `RuleViewModel` — per-rule display state with `ToModel()`/`From()` conversion to `ForwardRule`
  - Also contains `InverseBoolConverter`, `NonEmptyToVisibleConverter`, and `StringToBrushConverter` (hex string → SolidColorBrush, needed because WinUI doesn't auto-convert)
- **Models**: `Models.cs` — records: `UsbIpdState`, `UsbDeviceState` (with computed `IsConnected`/`IsBound`/`IsAttached`/`VidPid`), `ForwardRule`, `AppConfig`
- **Services**: `UsbIpdService.cs` (polling loop + attach logic), `ConfigService.cs` (persists config to `%APPDATA%\UsbBridge\config.json`)

**Data flow**: `usbipd` CLI subprocess → `UsbIpdService` (polling loop) → events (`OnAttached`/`OnDetached`/`OnFailed`/`OnDevicesUpdated`) → `MainViewModel` dispatches via `DispatcherQueue.TryEnqueue` → WinUI data binding → `MainWindow`

**Attach sequence** (in `UsbIpdService.PollLoop`): detach → bind (optionally `--force`) → settle delay → attach (`--wsl`, optional `--distribution`) → verify state

**Window lifecycle**: App starts as tray icon only → double-click opens window → close hides to tray (`AppWindow.Closing` + `e.Cancel = true`) → tray Exit calls `Environment.Exit(0)` (required for unpackaged WinUI)

## Key Dependencies

- **Microsoft.WindowsAppSDK** (`1.7.*`) — WinUI 3 framework
- **CommunityToolkit.Mvvm** (`8.*`) — MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **H.NotifyIcon.WinUI** (`2.*`) — system tray icon (uses `DoubleClickCommand` for tray double-click, `GeneratedIconSource` for icon)

## Important Details

- Target framework: `net10.0-windows10.0.19041.0`, unpackaged (`WindowsPackageType=None`), self-contained, x64
- No single-file publish — WinUI 3 limitation; output is a folder
- **Custom entry point**: `Program.cs` (`DISABLE_XAML_GENERATED_MAIN` define) — extracts embedded XBF resources from SDK PRI files via MRM.dll P/Invoke at startup (replaces the VS-only `ExpandPriContent` MSBuild task)
- **PRI workaround**: Build copies `Microsoft.UI.Xaml.Controls.pri` → `resources.pri` so MRT can find the "Microsoft.UI.Xaml" resource map needed by WinUI controls. Without VS, the standard PRI merge doesn't run
- Build targets `CopyLocalFilesOutputGroup`, `AddPriPayloadFilesToCopyToOutputDirectoryItems`, `_ExpandPriFiles`, `_ExpandProjectPriFile`, `_ExpandReferencePriFile` are overridden as no-ops in the csproj (VS build tools not available in CLI-only builds)
- Forward rules use glob patterns matching `vid:pid` (e.g., `04d8:*` matches all Microchip devices)
- `UsbIpdService` shells out to `usbipd` — requires usbipd-win installed on the host
- `UsbIpdService.GlobMatch` is public static, also used by `MainViewModel.UpdateDevices` to check if devices have matching rules
- Config stored as JSON at `%APPDATA%\UsbBridge\config.json`; `WslDistribution` field targets a specific WSL distro
- Default poll interval is 500ms; `ForceBind` per-rule controls whether `--force` is passed to `usbipd bind`
- DataTemplate commands use `ElementName=RootGrid` + `Tag` pattern to reach parent DataContext (WinUI doesn't support `RelativeSource AncestorType`)
- `MVVMTK0045` warnings are suppressed via `#pragma` — WinRT AOT compat not needed for this desktop app
