# SwitchNet

Windows 11 system-tray utility that switches company Wi‑Fi profiles and prefers the matching LAN (routes / gateway / interface metric).

Built for **Project switch-net**: hop between **GitLab LAN (.1)** and **CRM LAN (.3)** without manual `netsh` / route edits.

## Requirements

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- Saved WLAN profiles for each SSID (connect once in Windows Settings so the password is stored)
- **Administrator (UAC)** when applying routes / interface metrics

## Build

```powershell
cd switch-net
dotnet restore
dotnet build -c Release
```

Run from the build output:

```powershell
dotnet run --project src\SwitchNet\SwitchNet.csproj -c Release
```

Or start the exe:

```powershell
.\src\SwitchNet\bin\Release\net8.0-windows\SwitchNet.exe
```

Optional single-folder publish:

```powershell
dotnet publish src\SwitchNet\SwitchNet.csproj -c Release -r win-x64 --self-contained false -o .\publish
.\publish\SwitchNet.exe
```

## First-run setup

1. Start SwitchNet — a tray icon appears (green network glyph).
2. Right-click → **Settings…** (or double-click the tray icon).
3. Replace placeholders with your real values:

| Field | GitLab LAN example | CRM LAN example |
|-------|--------------------|-----------------|
| SSID | `COMPANY-GITLAB-WIFI` → your SSID | `COMPANY-CRM-WIFI` → your SSID |
| Subnet (CIDR) | `192.168.1.0/24` | `192.168.3.0/24` |
| Gateway | `192.168.1.1` | `192.168.3.1` |
| USB Wi‑Fi interface | often `Wi-Fi 2` (external stick only; not built-in) | same |

Observed SSID `1505` is noted as a placeholder candidate (role unknown — map it once you know which LAN it belongs to).

4. Config is stored at `%AppData%\SwitchNet\profiles.json` (tray → **Open config folder**).

### Ethernet + USB Wi‑Fi both up

Win11 usually prefers Ethernet for the default route. On profile apply, SwitchNet lowers the **USB Wi‑Fi** metric below Ethernet, optionally raises Ethernet metrics (`preferWifiOverEthernet`), binds the LAN subnet route to the USB IF, and (when enabled) prefers the profile default route via that gateway. See Project doc `docs/switch-net-app.md`.

## Usage

| Action | What happens |
|--------|----------------|
| Tray → **GitLab LAN (.1)** / **CRM LAN (.3)** | `netsh wlan connect` to that SSID, then apply routes/metric |
| SSID changes while auto-switch is on | Matching profile’s routes are applied (Wi‑Fi already connected) |
| Tray → **Settings…** | Edit SSIDs, subnets, gateways, auto-switch, interface name |

### UAC / elevation

- The app starts **unelevated** (`asInvoker`) so it can sit in the tray quietly.
- **Route and interface-metric changes require Administrator.** On profile switch, SwitchNet relaunches itself with `runas` / `--apply <profile-id>` and Windows shows a **UAC prompt**.
- Approve UAC to apply routing. Cancel leaves Wi‑Fi connect attempted but routes unchanged.
- To always run elevated, change `app.manifest` `requestedExecutionLevel` to `requireAdministrator` and rebuild (UAC at every launch).

### CLI (elevated apply only)

```powershell
SwitchNet.exe --apply gitlab-lan
SwitchNet.exe --apply crm-lan
```

## How it works

1. **Wi‑Fi** — `netsh wlan connect` on the configured **USB** adapter profile.
2. **Beat Ethernet** — set USB Wi‑Fi metric lower than Ethernet; raise competing Ethernet metrics when `preferWifiOverEthernet` is true.
3. **LAN route** — `route add <network> mask <mask> <gateway> … if <usb-if>` (metric 1).
4. **Default route** (optional per profile) — `route add 0.0.0.0 …` via the profile gateway on the USB IF.

## Placeholders you must replace

Until you fill real company values, connect/route steps will target these placeholders:

- SSIDs: `COMPANY-GITLAB-WIFI`, `COMPANY-CRM-WIFI`
- Subnets: `192.168.1.0/24`, `192.168.3.0/24`
- Gateways: `192.168.1.1`, `192.168.3.1`

Also confirm:

- Exact Windows WLAN profile names (`netsh wlan show profiles`) if they differ from the SSID → set `wlanProfileName` in config
- Wi‑Fi adapter display name (`Wi-Fi` vs `Wi-Fi 2`, etc.)

## Project layout

```
switch-net/
  SwitchNet.sln
  README.md
  src/SwitchNet/
    App.xaml(.cs)           Tray host + --apply entry
    Config/                 profiles.json + load/save
    Network/                wifi, routes, elevation, SSID watcher
    Tray/                   NotifyIcon menu
    UI/                     Settings window
```

## Notes

- This folder is standalone. It does not modify Chat2DB client/server code.
- Build and run on Windows 11; this source tree is authored for `net8.0-windows` (WPF + WinForms NotifyIcon).
