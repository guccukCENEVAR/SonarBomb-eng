# SonarBomb

[![Counter-Strike 2](https://img.shields.io/badge/CS2-Plugin-FF6B00?style=flat&logo=counter-strike)](https://store.steampowered.com/app/730/CounterStrike_2/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.362-00d4aa?style=flat)](https://github.com/roflmuffin/CounterStrikeSharp)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)

**Tactical Sonar Grenade for Hide and Seek** — A Counter-Strike 2 plugin that turns decoy grenades into sonar devices that detect hidden enemies.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Commands](#commands)
- [How It Works](#how-it-works)
- [Building](#building)
- [Project Structure](#project-structure)
- [Author](#author)
- [License](#license)

---

## Overview

SonarBomb is a [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin designed for Hide and Seek game modes. When a player throws a "Sonar Grenade" (decoy), it scans for enemies within line of sight. If an enemy is detected, the thrower hears an audio cue.

## Features

- **Sonar Detection** — Scans a 3000 unit radius for enemies when a decoy is thrown
- **Wall Check** — Uses engine-level ray tracing via [FUNPLAY Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace) to only detect enemies in direct line of sight
- **Market Control** — Disables the buy menu when active to prevent abuse
- **Admin Commands** — Distribute sonar grenades to specific players or teams

## Requirements

| Requirement | Link |
|-------------|------|
| Counter-Strike 2 + CounterStrikeSharp | [GitHub](https://github.com/roflmuffin/CounterStrikeSharp) |
| FUNPLAY Ray-Trace (Metamod) | [GitHub](https://github.com/FUNPLAY-pro-CS2/Ray-Trace) |
| .NET 8.0 | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |

## Installation

1. Download the latest release or build from source (see [Building](#building))
2. Place `SonarBomb.dll` in `game/csgo/addons/counterstrikesharp/plugins`
3. Install and load the [FUNPLAY Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace) Metamod module
4. Restart the server or run: `css_plugins load SonarBomb`

## Commands

| Command | Description | Permission |
|---------|-------------|------------|
| `!sonarbomb` | Toggle plugin on/off (enables/disables market) | `@css/generic` |
| `!sonar [target]` | Give sonar grenade(s) to player(s) | `@css/generic` |
| `css_sonar [target]` | Console command — same as `!sonar` | `@css/generic` |

### Target Arguments

| Target | Description |
|--------|-------------|
| `@me` | Give to yourself (default) |
| `@all` | Give to all alive players |
| `@t` | Give to Terrorists |
| `@ct` | Give to Counter-Terrorists |
| `[name]` | Give to player matching name (partial match) |

## How It Works

1. Admin enables the plugin with `!sonarbomb` — market is disabled
2. Admin distributes sonar grenades with `!sonar @ct` (or other targets)
3. Players throw decoys as "Sonar Grenades"
4. On impact, the plugin performs multi-point ray traces (head, chest, waist) to check for enemies in line of sight
5. If an enemy is detected within 3000 units, the thrower hears a blink/notification sound
6. The decoy projectile is removed for a clean effect

## Building

```bash
git clone https://github.com/YOUR_USERNAME/SonarBomb-eng.git
cd SonarBomb-eng
dotnet build -c Release
```

Output: `bin/Release/net8.0/SonarBomb.dll`

## Project Structure

```
SonarBomb-eng/
├── SonarBomb.cs      # Main plugin logic
├── RayTrace.cs       # FUNPLAY Ray-Trace C# wrapper
├── SonarBomb.csproj  # Project file
└── README.md
```

## Author

**guccukCENEVAR**