<p align="center">
  <img alt="ASA Server Manager Control Banner" src="https://capsule-render.vercel.app/api?type=waving&height=220&color=0:040b14,35:0b1b2d,68:00f7ff,100:17081f&text=ASA%20Server%20Manager%20Control&fontColor=ffffff&fontSize=34&fontAlignY=38&desc=Neon%20control%20panel%20for%20multiple%20ARK%3A%20Survival%20Ascended%20servers&descAlignY=58&animation=twinkling" />
</p>

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

<p align="center">
  <img alt="Debian 13 Required" src="https://img.shields.io/badge/Debian-13-a80030?style=for-the-badge&logo=debian&logoColor=white" />
  <img alt="Root Required" src="https://img.shields.io/badge/Install-Root_Access_Required-cb2027?style=for-the-badge&logo=gnubash&logoColor=white" />
  <img alt="Router Reachable" src="https://img.shields.io/badge/Network-Router_Reachable-0aa6ff?style=for-the-badge&logo=wireguard&logoColor=041018" />
</p>

```text
┌────────────────────────────────────────────────────────────────────┐
│  CONTROL NODE                                                     │
│  single Debian 13 machine                                         │
│  router reachable / port-forwardable if behind NAT                │
│  install script must run as root                                  │
└────────────────────────────────────────────────────────────────────┘
```

<p align="center">
  <img alt="System Requirements" src="https://img.shields.io/badge/IMPORTANT-System_Requirements-cb2027?style=for-the-badge&logoColor=white" />
</p>

## System Requirements

This app is intended to run on one dedicated Debian machine.

Required:

- Debian `13`
- `root` access to start the install script
- a machine that is router-accessible
- ability to expose or forward required ports if the machine sits behind NAT
- reachable networking for the web panel

Recommended minimum:

- `2` CPU cores
- `2 GB` RAM
- `10 GB` disk

Still workable:

- `1` CPU core
- `1 GB` RAM
- `5 GB` disk

Disk notes:

- `10 GB` is enough
- `10-15 GB` is a comfortable target
- `5 GB` can still work for a very small/light setup

## Proposed Setup

Known-good setup used for this project:

- Debian `13` LXC container
- running on Proxmox
- one dedicated machine/container for the control panel

Short version:

- install on a single Debian 13 box
- make sure the box is reachable on your network or from the internet
- forward the needed ports if you are behind NAT
- run the installer as `root`

## Quick Install

```bash
apt update && apt upgrade -y && apt install curl -y
bash -c "$(curl -fsSL https://raw.githubusercontent.com/DragoQC/ASA_Server_Manager_Control/main/setup-manager-webapp.sh)"
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

## Stack

- `.NET 10.0`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.0-rc.2.25502.107`
- `Microsoft.EntityFrameworkCore.Sqlite 10.0.0-rc.2.25502.107`
- Blazor Web App
- SQLite
- Tailwind CSS v4 build hook

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
- prepares `/opt/asa-control`
- installs base Linux deps
- installs `.NET SDK 10.0.100-rc.2.25502.107`
- clones `https://github.com/DragoQC/ASA_Server_Manager.git`
- publishes `managerwebapp`
- creates `asa-control-webapp.service`
- starts the manager app on port `8010`

Default runtime layout:

- `/opt/asa-control/webapp/src`
- `/opt/asa-control/webapp/publish`
- `/etc/systemd/system/asa-control-webapp.service`

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
