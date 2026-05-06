# 🛰️ SonarBomb (CS2 Plugin)

[![Counter-Strike 2](https://img.shields.io/badge/CS2-Plugin-FF6B00?style=flat&logo=counter-strike)](https://store.steampowered.com/app/730/CounterStrike_2/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.367-00d4aa?style=flat)](https://github.com/roflmuffin/CounterStrikeSharp)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)

**Tactical Sonar Bomb for Hide and Seek** — A Counter-Strike 2 plugin that turns the decoy grenade into a sonar device that detects hidden enemies and makes them invisible.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Commands](#commands)
- [How It Works](#how-it-works)
- [Building](#building)
- [Project Structure](#project-structure)
- [Author](#author)

---

## 🎯 Overview

SonarBomb is a [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin designed for Hide and Seek modes. When an admin throws a "Sonar Bomb" (decoy), it scans for hidden enemies within a specific area and applies a **wallhack-like glow effect** to detected players (invisible + light).

## ✨ Features

- **🔍 Sonar Detection** — Scans all enemies within a 3000 unit radius when a decoy is thrown
- **🌍 Wall Check** — Engine level ray tracing via [FUNPLAY Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace)
- **👻 Wallhack Glow** — Detected players become invisible + a glow effect that passes through walls
- **🔌 Clean Exit** — All active glow effects are instantly cleared when the plugin is disabled
- **⚙️ Admin Commands** — Distribute sonar bombs to specific players or teams

## 📦 Requirements

| Requirement | Link |
|------------|----------|
| Counter-Strike 2 + CounterStrikeSharp 1.0.367+ | [GitHub](https://github.com/roflmuffin/CounterStrikeSharp) |
| FUNPLAY Ray-Trace (Metamod) | [GitHub](https://github.com/FUNPLAY-pro-CS2/Ray-Trace) |
| .NET 8.0 | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |

## 📥 Installation

1. Download the latest release or build from source (see [Building](#building))
2. Place the `SonarBomb.dll` file in the `game/csgo/addons/counterstrikesharp/plugins` folder
3. Install and load the [FUNPLAY Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace) Metamod module
4. Restart the server or run the command `css_plugins load SonarBomb`

## ⌨️ Commands

| Command | Description | Permission |
|-------|----------|-------|
| `!sonarbomb` | Toggle plugin (manages market) | `@css/generic` |
| `!sonar [target]` | Give sonar bomb(s) to player(s) | `@css/generic` |
| `css_sonar [target]` | Console command — same as `!sonar` | `@css/generic` |

### Target Parameters

| Target | Description |
|-------|----------|
| `@me` | Give to self (default) |
| `@all` | Give to all alive players |
| `@t` | Give to Terrorists |
| `@ct` | Give to Counter-Terrorists |
| `[name]` | Give to player with matching name (partial match) |

## 🎮 How It Works

1. Admin **activates** the plugin via `!sonarbomb`
2. Admin distributes sonar bombs using `!sonar @t` (or other targets)
3. Players throw the decoys
4. When the sonar explodes:
   - Scans **all hidden players** within 3000 units
   - Validates true visibility using multi-point check (head, chest, waist)
   - **For each detected player:**
     - 👻 Becomes invisible
     - 🌟 Wallhack glow appears around them
     - ⏱️ Returns to normal after 7 seconds
5. The decoy projectile is automatically removed

## 🔨 Building

```bash
git clone https://github.com/YOUR_USERNAME/SonarBomb.git
cd SonarBomb
dotnet build -c Release
```

Output: `bin/Release/net8.0/SonarBomb.dll`

## 📁 Project Structure

```
SonarBomb/
├── SonarBomb.cs      # Core plugin logic
├── RayTrace.cs       # FUNPLAY Ray-Trace C# wrapper
├── SonarBomb.csproj  # Project file
└── README.md         # This file
```

## 👤 Author

**guccukCENEVAR**

---

*Designed for hide and seek games, to give players a fair chance!* 🎯
