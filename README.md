# WSL USB Bridge

Automatically forward USB devices from Windows to WSL. Plug in a device, and it shows up in your Linux environment, no commands needed.

## How it works

1. Install [usbipd-win](https://github.com/dorssel/usbipd-win)
2. Download the [latest release](../../releases/latest) and run `UsbBridge.exe`
3. Click a device and toggle **Bridge automatically**
4. Done. That device will be forwarded to WSL whenever it's plugged in

The app lives in your system tray. Double-click the icon to open the window, right-click to exit.

## Features

- Runs in the system tray, out of the way
- Automatic forwarding - set it once, forget about it
- Pattern rules for forwarding entire families of devices (e.g. all devices from the same manufacturer)
- Start with Windows option
- Pick which WSL distro to forward to

## Requirements

- Windows 10 or 11
- [usbipd-win](https://github.com/dorssel/usbipd-win)
- WSL 2
