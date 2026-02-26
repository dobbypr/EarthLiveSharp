# EarthLiveSharp

[![Build and Test](https://github.com/dobbypr/EarthLiveSharp/actions/workflows/build.yml/badge.svg)](https://github.com/dobbypr/EarthLiveSharp/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/dobbypr/EarthLiveSharp.svg)](LICENSE)

A cross-platform .NET 8 daemon that fetches real-time satellite imagery and sets it as your desktop wallpaper.  
Supports **Arch Linux**, **Linux Mint**, **Ubuntu**, **Fedora**, and other modern Linux distributions, as well as Windows.

## Satellite Sources

| Source | Coverage | Update Frequency |
|--------|----------|-----------------|
| **Himawari-9** (RAMMB/CIRA SLIDER) | Asia-Pacific full disk | ~10 min |
| **NASA EPIC** (DSCOVR) | Whole Earth from L1 Lagrange point | ~hourly |

> **Note:** The original Himawari-8 NICT endpoint (`himawari8-dl.nict.go.jp`) was decommissioned on 26 November 2025 when Himawari-9 became the primary operational satellite. This version uses the RAMMB/CIRA SLIDER service for Himawari-9 imagery.

## Installation

### Arch Linux (AUR)

```bash
git clone https://aur.archlinux.org/earthlivesharp.git
cd earthlivesharp
makepkg -si
```

### Generic Linux / Self-Contained Binary

Download the latest `earthlivesharp-linux-x64` artifact from the [GitHub Actions](https://github.com/dobbypr/EarthLiveSharp/actions) page, or build from source:

```bash
git clone https://github.com/dobbypr/EarthLiveSharp.git
cd EarthLiveSharp
dotnet publish src/EarthLiveSharp.Cli -r linux-x64 --self-contained -c Release -o out/
./out/earthlivesharp --help
```

### .NET Runtime Required (non-self-contained)

```bash
dotnet publish src/EarthLiveSharp.Cli -c Release -o out/
dotnet out/earthlivesharp.dll
```

## Configuration

Copy `config.example.json` to `~/.config/earthlivesharp/config.json` and edit:

```json
{
  "satellite": "Himawari9",
  "intervalMinutes": 20,
  "autostart": false,
  "setWallpaper": true,
  "size": 1,
  "zoom": 100
}
```

| Field | Values | Description |
|-------|--------|-------------|
| `satellite` | `"Himawari9"`, `"NasaEpic"` | Imagery source |
| `intervalMinutes` | integer | Polling interval in minutes |
| `autostart` | bool | Create XDG autostart entry |
| `setWallpaper` | bool | Set desktop wallpaper after download |
| `size` | 1, 2, 4, 8, 16 | Tile grid dimension (1 = lowest resolution) |
| `zoom` | 1–100 | Output scale percentage |
| `saveTexture` | bool | Save wallpaper snapshots |
| `saveDirectory` | path | Directory for snapshots |
| `saveMaxCount` | integer | Maximum snapshots to keep |

## Supported Desktop Environments

Wallpaper setting is automatic based on `XDG_CURRENT_DESKTOP`:

| DE | Mechanism |
|----|-----------|
| GNOME / Unity / Ubuntu | `gsettings org.gnome.desktop.background` |
| Cinnamon (Linux Mint) | `gsettings org.cinnamon.desktop.background` |
| MATE | `gsettings org.mate.background` |
| XFCE | `xfconf-query` |
| KDE Plasma | `plasma-apply-wallpaperimage` |
| Sway | `swaymsg output '*' bg ...` |
| Hyprland | `hyprctl hyprpaper` |
| Generic X11 | `feh --bg-fill` |
| Windows | Win32 `SystemParametersInfo` |

## systemd User Service

```bash
cp packaging/earthlivesharp.service ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now earthlivesharp
```

## Building from Source

Requirements: .NET 8 SDK

```bash
dotnet build EarthLiveSharp.sln
dotnet test EarthLiveSharp.sln
```

## Screenshots

![screenshot1](https://cloud.githubusercontent.com/assets/6072743/23821657/7d53554e-0674-11e7-8ccc-260070261967.png)

## License

MIT
