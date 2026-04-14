<p align="center">
  <img alt="ASA Server Manager Control" src="https://img.shields.io/badge/ASA_Server_Manager-Control_Panel-00f7ff?style=for-the-badge&logo=windows-terminal&logoColor=041018&labelColor=041018" />
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10_RC-7df9ff?style=for-the-badge&logo=dotnet&logoColor=041018&labelColor=041018" />
  <img alt="Blazor" src="https://img.shields.io/badge/Blazor-Interactive_Server-37f3ff?style=for-the-badge&logo=blazor&logoColor=041018&labelColor=041018" />
  <img alt="SQLite" src="https://img.shields.io/badge/SQLite-Ready-7affd8?style=for-the-badge&logo=sqlite&logoColor=041018&labelColor=041018" />
  <img alt="ARK ASA" src="https://img.shields.io/badge/ARK-Survival_Ascended-a6ff00?style=for-the-badge&logoColor=041018&labelColor=041018" />
</p>

<h1 align="center">ASA Server Manager Control</h1>

<p align="center">
  Neon control panel for multiple ARK: Survival Ascended servers.
</p>

<p align="center">
  One central panel. Multiple remote server panels. API + WebSocket state tracking.
</p>

## Quick Install

```bash
apt update && apt upgrade -y && apt install curl -y
bash -c "$(curl -fsSL https://raw.githubusercontent.com/DragoQC/ASA_Server_Manager/main/setup-server-webapp.sh)"
```

## What This Repo Is

`ASA_Server_Manager_Control` is the central manager app.

The idea:

- each ASA server runs its own local web panel
- this repo runs beside them as the global control surface
- the control app connects to those remote panels through API + WebSockets
- the goal is one place to see server state, auth, alerts, and fleet-level actions

Short version:

`ASA_Server_Manager_server` = per-server panel  
`ASA_Server_Manager_Control` = central manager for all of them

## Current State

This repo is already set up with the base manager foundation:

- Blazor Web App on `.NET 10`
- interactive server components
- SQLite persistence
- ASP.NET Core Identity auth
- remote server records
- email settings storage
- neon dashboard shell + layout
- Linux install script + `systemd` service

Current dashboard messaging in the app is aligned with that direction:

- auth is wired
- email settings entity is ready
- remote server records are ready

What is not built yet:

- full remote server CRUD UI
- API handshake flow with remote panels
- live WebSocket server status streams
- full fleet dashboard widgets and actions

## Repo Layout

```text
ASA_Server_Manager_Control/
├── managerwebapp/
│   ├── Components/
│   ├── Data/
│   │   ├── Configurations/
│   │   ├── Entities/
│   │   ├── AppDbContext.cs
│   │   └── ApplicationUser.cs
│   ├── Properties/
│   ├── Styles/
│   ├── wwwroot/
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Program.cs
│   └── managerwebapp.csproj
├── setup-manager-webapp.sh
├── ASA_Server_Manager_Control.sln
└── README.md
```

## Stack

- `.NET 10.0`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.0-rc.2.25502.107`
- `Microsoft.EntityFrameworkCore.Sqlite 10.0.0-rc.2.25502.107`
- Blazor Web App
- SQLite
- Tailwind CSS v4 build hook

## Data Model

Current DB shape is intentionally small:

- `ApplicationUser` for auth
- `EmailSettings` for manager-side SMTP config
- `RemoteServers` for remote panel connections

`RemoteServers` currently stores:

- `IpAddress`
- `ApiKeyHash`

That fits the control-panel role: keep remote server connection info here, not server runtime files.

## Local Development

Run the manager app:

```bash
dotnet watch --project managerwebapp/managerwebapp.csproj
```

Notes:

- launch settings use dynamic localhost ports in development
- SQLite connection string is `Data Source=Data/managerwebapp.db`
- the DB is created automatically with `EnsureCreated()`

## Tailwind

The project has a build target that compiles Tailwind only if this binary exists:

```text
managerwebapp/tools/tailwindcss
```

Input and output:

- input: `managerwebapp/Styles/app.css`
- output: `managerwebapp/wwwroot/app.css`

If the binary is missing, the app still builds using the committed CSS output.

## Installer

Manager installer script:

```text
setup-manager-webapp.sh
```

What it does:

- creates `asa_manager_web_app`
- prepares `/opt/asa-manager`
- installs base Linux deps
- installs `.NET SDK 10.0.100-rc.2.25502.107`
- clones `https://github.com/DragoQC/ASA_Server_Manager.git`
- publishes `managerwebapp`
- creates `asa-manager-webapp.service`
- starts the manager app on port `8010`

Default runtime layout:

- `/opt/asa-manager/webapp/src`
- `/opt/asa-manager/webapp/publish`
- `/etc/systemd/system/asa-manager-webapp.service`

Default service URL:

```text
http://<host-ip>:8010
```

## Runtime Notes

This app is not the per-server controller itself.

It is the control plane intended to sit beside other installed ASA panels and talk to them remotely.

That means this repo should stay focused on:

- shared auth
- central remote server registry
- fleet visibility
- notifications
- remote server state
- cross-server orchestration

Not on:

- local ASA install/runtime file editing for one specific machine
- single-server-only workflows already handled by the server panel

## Design Direction

The UI is intentionally neon:

- dark sci-fi surface
- cyan/turquoise highlights
- glowing network background
- control-room style layout

This is the manager app, so the look should feel like a central command panel for an ASA fleet.

## Near-Term Goal

The next logical pieces for this repo are:

- register remote server panels securely
- validate API connectivity
- open WebSocket connections to each remote panel
- show live status like up, down, starting, stopping
- surface all servers in one central dashboard

That is the core purpose of `ASA_Server_Manager_Control`.
