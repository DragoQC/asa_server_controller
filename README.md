<p align="center">
  <img alt="ASA Server Controller Banner" src="https://capsule-render.vercel.app/api?type=waving&height=220&color=0:05070d,35:17081f,68:a855f7,100:2a0f3f&text=ASA%20Server%20Controller&fontColor=ffffff&fontSize=34&fontAlignY=38&desc=Neon%20control%20panel%20for%20multiple%20ARK%3A%20Survival%20Ascended%20servers&descAlignY=58&animation=twinkling" />
</p>

<p align="center">
  <img alt="ASA Server Controller" src="https://img.shields.io/badge/ASA_Server_Controller-Control_Panel-d8b4fe?style=for-the-badge&logo=windows-terminal&logoColor=05070d&labelColor=17081f" />
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10_RC-e9d5ff?style=for-the-badge&logo=dotnet&logoColor=05070d&labelColor=17081f" />
  <img alt="Blazor" src="https://img.shields.io/badge/Blazor-Interactive_Server-c084fc?style=for-the-badge&logo=blazor&logoColor=05070d&labelColor=17081f" />
  <img alt="SQLite" src="https://img.shields.io/badge/SQLite-Ready-d8b4fe?style=for-the-badge&logo=sqlite&logoColor=05070d&labelColor=17081f" />
  <img alt="ARK ASA" src="https://img.shields.io/badge/ARK-Survival_Ascended-f0abfc?style=for-the-badge&logoColor=05070d&labelColor=17081f" />
</p>

<h1 align="center">🦖 ASA Server Controller 🦕</h1>

<p align="center">
  Neon control panel for multiple ARK: Survival Ascended servers.
</p>

<p align="center">
  One central panel. Multiple remote server panels. API + WebSocket state tracking.
</p>

<p align="center">
  <img alt="Debian 13 Required" src="https://img.shields.io/badge/Debian-13-a80030?style=for-the-badge&logo=debian&logoColor=white" />
  <img alt="Root Required" src="https://img.shields.io/badge/Install-Root_Access_Required-cb2027?style=for-the-badge&logo=gnubash&logoColor=white" />
  <img alt="Router Reachable" src="https://img.shields.io/badge/Network-Router_Reachable-c084fc?style=for-the-badge&logo=wireguard&logoColor=05070d" />
</p>

```text
┌────────────────────────────────────────────────────────────────────┐
│  CONTROL NODE                                                      │
│  single Debian 13 machine                                          │
│  router reachable / port-forwardable if behind NAT                 │
│  install script must run as root                                   │
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
bash -c "$(curl -fsSL https://raw.githubusercontent.com/DragoQC/asa_server_controller/main/setup-asa-server-controller.sh)"
```

## What The Installer Does

The installer is meant to be a simple two-line setup.

It will:

- install the base Linux dependencies needed by the app
- install the required `.NET 10` SDK/runtime pieces
- prepare the `/opt/asa-control` runtime folders
- clone and publish the manager web app
- create the `systemd` service for the control app
- start the app for you
- let you connect to the panel on port `8010`

## What The App Does For You

Once installed, the control app lets you:

- configure the WireGuard VPN for your control node
- create remote registration / invitation links for other ARK server nodes
- manage NFS sharing together with the VPN setup
- start, stop, and send RCON commands to remotely connected nodes
- save your CurseForge API key so public users can see mod information
- cache mod metadata locally so it does not need to be refetched every time
- show a public page with server list, map name, server name, player counts, and mod info
- manage the shared ARK cluster setup from one control panel

## Ports

Ports used by this control app setup:

- `8010/TCP`
  - manager web app
  - default control panel URL: `http://<host-ip>:8010`
- `51820/UDP`
  - WireGuard VPN port when VPN is enabled
  - this is the default value
  - if you change the WireGuard listen port in Cluster setup, forward that port instead

If the control node is behind NAT:

- forward `8010/TCP` if you want direct web access to the control panel
- forward the configured WireGuard UDP port if you want remote servers to join through VPN
- make sure the machine is router-reachable and not blocked by firewall rules

## What This Repo Is

`asa_server_controller` is the central manager app.

The idea:

- each ASA server runs its own local web panel
- this repo runs beside them as the global control surface
- the control app connects to those remote panels through API + WebSockets
- the goal is one place to see server state, auth, alerts, and fleet-level actions

Short version:

`asa_server_api_node` = per-server panel  
`asa_server_controller` = central manager for all of them

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
dotnet watch --project asa_server_controller/asa_server_controller.csproj
```

Notes:

- launch settings use dynamic localhost ports in development
- SQLite connection string is `Data Source=Data/managerwebapp.db`
- the DB is created automatically with `EnsureCreated()`

## Tailwind

The project has a build target that compiles Tailwind only if this binary exists:

```text
asa_server_controller/tools/tailwindcss
```

Input and output:

- input: `asa_server_controller/Styles/app.css`
- output: `asa_server_controller/wwwroot/app.css`

If the binary is missing, the app still builds using the committed CSS output.

## Installer

Manager installer script:

```text
setup-asa-server-controller.sh
```

What it does:

- creates `asa_manager_web_app`
- prepares `/opt/asa-control`
- installs base Linux deps
- installs `.NET SDK 10.0.100-rc.2.25502.107`
- clones `https://github.com/DragoQC/asa_server_controller.git`
- publishes `asa_server_controller`
- creates `asa-webapp.service`
- starts the manager app on port `8010`

Default runtime layout:

- `/opt/asa-control/webapp/src`
- `/opt/asa-control/webapp/publish`
- `/etc/systemd/system/asa-webapp.service`

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
- fuchsia / purple highlights
- glowing network background
- control-room style layout

This is the manager app, so the look should feel like a central command panel for an ASA fleet.
