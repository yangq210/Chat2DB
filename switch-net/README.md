# SwitchNet

Windows 11 system-tray utility for **three LANs**: everyday **wired Ethernet**, plus temporary **GitLab** / **CRM** hops on an external USB Wi‑Fi stick.

## Tray actions

| Action | Behavior |
|--------|----------|
| **默认有线** | Everyday default — Ethernet metric wins; clear GitLab/CRM routes; disconnect USB Wi‑Fi (configurable) |
| **GitLab** | Connect SSID `1505`, prefer USB for `192.168.1.0/24` via `192.168.1.1` |
| **CRM** | Connect SSID `CU_bDXR`, prefer USB for `192.168.3.0/24` via `192.168.3.1` |

Resting state is **wired**. Do not leave Wi‑Fi preferred after GitLab/CRM work — pick **默认有线**.

## Requirements

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- Saved WLAN profiles for `1505` and `CU_bDXR`
- **Administrator (UAC)** when applying routes / interface metrics
- External USB Wi‑Fi (MERCURY Wireless N) — not the laptop built-in adapter

## Build / run

```powershell
cd switch-net
dotnet restore
dotnet run --project src\SwitchNet\SwitchNet.csproj -c Release
```

```powershell
dotnet publish src\SwitchNet\SwitchNet.csproj -c Release -r win-x64 --self-contained false -o .\publish
.\publish\SwitchNet.exe
```

## First-run setup

1. Start SwitchNet — tray icon appears.
2. Set **USB Wi‑Fi interface** name from `netsh interface show interface` (often `Wi-Fi 2`).
3. Confirmed Wi‑Fi profiles:

| Profile | SSID | Subnet | Gateway | Sample host |
|---------|------|--------|---------|-------------|
| GitLab | `1505` | `192.168.1.0/24` | `192.168.1.1` | `192.168.1.16` |
| CRM | `CU_bDXR` | `192.168.3.0/24` | `192.168.3.1` | `192.168.3.175` |

4. Wired Ethernet subnet/gateway still TBD — paste `ipconfig /all` for the Ethernet adapter when convenient. **默认有线** still restores metric priority without it.
5. Config: `%AppData%\SwitchNet\profiles.json`

## UAC / elevation

App starts unelevated. Profile apply relaunches with `runas` / `--apply <profile-id>` (UAC prompt).

```powershell
SwitchNet.exe --apply default-wired
SwitchNet.exe --apply gitlab-lan
SwitchNet.exe --apply crm-lan
```

## How it works

**默认有线:** Ethernet metric low, USB Wi‑Fi metric high, delete temporary Wi‑Fi LAN/default routes, optional `netsh wlan disconnect`.

**GitLab / CRM:** `netsh wlan connect`, raise Ethernet metric / lower USB Wi‑Fi, bind LAN (+ optional default) routes to the USB IF. Cleared when returning to 默认有线 or switching the other Wi‑Fi profile.

## Project layout

```
switch-net/
  SwitchNet.sln
  README.md
  src/SwitchNet/
    App.xaml(.cs)
    Config/                 profiles.json + load/save
    Network/                wifi, routes, elevation, SSID watcher
    Tray/                   NotifyIcon menu
    UI/                     Settings window
```

Standalone folder — does not modify Chat2DB client/server code.
