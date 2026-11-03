<div align="center">

# Ringo Dead Cells Multiplayer

**Online co-op for up to four players in Dead Cells: clear biomes together, fight bosses together, take the door together.**

An open-source co-op mod written from scratch on top of [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core).

**English** · [简体中文](docs/README.zh-CN.md) · [繁體中文](docs/README.zh-TW.md) · [日本語](docs/README.ja.md) · [Русский](docs/README.ru.md) · [Español](docs/README.es.md)

[![Release](https://img.shields.io/github/v/release/Ringophilia/Ringo-DeadCell-MP?include_prereleases&label=release)](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Game](https://img.shields.io/badge/Dead%20Cells-v35-c0392b)
![Players](https://img.shields.io/badge/players-2--4-27ae60)
![Status](https://img.shields.io/badge/status-early%20preview-orange)

[Features](#features) · [Install](#install) · [How to play](#how-to-play) · [Co-op rules](#co-op-rules) · [Status](#status) · [How it works](#how-it-works) · [Building](#building-from-source) · [Troubleshooting](docs/TROUBLESHOOTING.md)

</div>

> [!WARNING]
> **Early preview.** Every feature passes automated tests that run two games on one machine, but the mod has **not yet been played between two real Steam accounts**, and nobody has finished a full run with it. Expect crashes, failed connections or desyncs, and back up your save first. See [Status](#status).

## Features

| | |
|---|---|
| 🎮 **One shared world** | Each player controls their own hero in the same run; mobs, deaths and drops stay in sync |
| 🏠 **Room lobby** | Pick **Multiplayer** in the main menu to browse friends' and public rooms, then create, join or manage one |
| 👑 **Host controls** | See who is in the room, invite friends, remove a player, lock the room, switch between public and friends only |
| 🚪 **Door gathering** | Press a door to wait at it and cancel any time; you only leave together, through the same door |
| 🛟 **Downed and rescue** | Running out of life puts you down first, and the others have 30 seconds to get you up; the camera follows one of them meanwhile |
| 🏷️ **See each other** | Every player's name floats above their hero, and they wear their own outfit when your game has it unlocked too |
| ⚖️ **Balanced for the team** | Mobs get more life for every extra player, also when someone joins or leaves mid-level; gear on the ground is shared, scrolls, cells and gold are personal |
| 📈 **Progress-safe** | Runs use the lowest progress among the players, existing unlocks in your save are never rewritten, and new ones are saved as usual |
| 🗓️ **Daily challenge and Boss Rush** | The host starts the daily challenge from the lobby, or Boss Rush from its door in the game, and everyone goes along; a run played together sends no score to the leaderboard |
| 🔌 **Join any time, reconnect** | The host can start alone and friends join mid-run; dropped links reconnect; after a crash you return to your level with your gear, and if the host leaves, another player takes the room over |
| 🌐 **13 languages** | The lobby and every message follow the game's language |

## Install

**Every player needs:**

1. Dead Cells for PC on Steam, tested on v35.
2. [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core) (DCCM), tested on 35.14.2.
3. `RingoMP-v0.4.0.zip` from [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases).
4. Unzip it and put the `RingoMP` folder into `coremod\mods\` inside the game folder:

```text
Dead Cells\
└─ coremod\
   └─ mods\
      └─ RingoMP\
         ├─ RingoMP.dll
         └─ modinfo.json
```

5. Back up your save: `Steam\userdata\<your id>\588650\remote`.
6. Start the game with `coremod\core\host\startup\DeadCellsModding.exe`.

> [!TIP]
> To accept invites **while the game is closed**, set this as the game's launch option in Steam (with your own path). This has not been tested yet:
>
> ```text
> "D:\your path\Dead Cells\coremod\core\host\startup\DeadCellsModding.exe" %command%
> ```

Something not working? See [Troubleshooting](docs/TROUBLESHOOTING.md).

## How to play

1. Choose **Multiplayer** in the main menu to open the lobby.
2. Choose **Create a room**, then **Invite a friend**, or choose a room in the list to join it.
3. Once your friends are in, the host chooses **Start a new run**, **Continue the saved run** or **Start the daily challenge**, and everyone follows automatically. Boss Rush starts from its door in the game, for everyone too.

The lobby is a menu like the game's options: click with the mouse, or move with the keys or a gamepad and confirm as in any other menu. During a run, the pause menu has a **Multiplayer** entry that opens the same lobby, to invite someone, lock the room or leave it.

## Co-op rules

**Leaving a level**

- Pressing a door makes you wait there. Press again or walk 3 cells away to cancel, so a mistaken press costs nothing.
- Nobody leaves until everyone waits at the same door. Players who are down count as there.
- Ways out without a door, like falls and hidden passages, take one player first and the others follow automatically.
- Doors into side rooms (challenge rooms, the bank and the like) work the same way: you go in together and come back out together.

**Dying**

- When you go down, another player stands next to you and holds interact for 1.5 seconds to get you up with half your life. Your camera follows a player still standing while you wait.
- If nobody gets you up within 30 seconds, you come back in the next level.
- The run only ends when everyone is down.

**Resources**

| Resource | Rule |
|---|---|
| Scrolls, cells, gold | One each, everyone picks up their own |
| Weapons, skills and amulets on the ground | Shared: when one player takes it, it is gone for the others; what you swap out stays on the ground for them |
| Starting armory | Each player picks their own |
| Mob drops | One each |

**Mob strength** (starting values, to be tuned after real play): each player after the first adds the same again. Damage is unchanged.

| Kind | 2 players | 3 players | 4 players |
|---|---|---|---|
| Regular | ×1.5 | ×2.0 | ×2.5 |
| Elite | ×1.7 | ×2.4 | ×3.1 |
| Boss | ×1.8 | ×2.6 | ×3.4 |

**Progress**

- A run uses the lowest progress among the players: the unlocks everyone has, and the fewest boss cells. That is what lets every game build the same levels.
- This only affects what is loaded. Unlocks already in your save are never rewritten, and what you unlock during the run is saved by the game as usual, into each player's own save.

## Status

**Passing automated tests** (two to four games on one machine, some with 5% packet loss and 80 ms lag)

- [x] Heroes, mobs, animations and hits in sync
- [x] Door gathering, cancelling, different doors, several levels in a row
- [x] Going down, rescue, coming back after the window, a new run after a wipe
- [x] Shared gear, swapped-out gear
- [x] The same levels at the lower progress (simulated on one save)
- [x] Reconnecting after a dropped link, going on alone after the others leave
- [x] Joining mid-run, coming back after a crash with your gear and its affixes
- [x] Going into a side room together and back out, in sync on both sides
- [x] Three and four players: everyone sees everyone, one door for all, a player leaving mid-run
- [x] The daily challenge with the same seed for everyone, and following the host into Boss Rush
- [x] Creating and listing rooms on real Steam
- [x] The lobby in the main menu and the pause menu (checked on pictures the tests take)
- [x] All menu text in 13 languages fits the game's fonts

**Not verified yet**

- [ ] P2P connections and invites between two real Steam accounts
- [ ] A full run played by people
- [ ] The lobby with a real mouse and gamepad
- [ ] The bank and every kind of side room (tested with the first level's rooms)
- [ ] Writing and reading back a real save in co-op
- [ ] Normal boss fights against the Giant, the Queen, Dooku and the Collector
- [ ] The host taking a room back after a crash, and meeting in the level of a continued save (written, not tested yet)
- [ ] The others' outfits on screen
- [ ] Training from its door, and a daily score kept off the leaderboard (written, not tested yet)
- [ ] Another player taking the room over when the host leaves with three or more in it

**Known limits**

- Four players at most.
- No custom mode.

Issues are welcome on [GitHub](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues). Please attach the newest log from `coremod\logs`.

## How it works

Dead Cells makes about 15,600 random calls without a seed, and its levels also depend on what is in the save, so lockstep is out. In this mod **the host owns the world, and each player owns their hero**:

```mermaid
flowchart LR
    subgraph Host["Host"]
        HW["World: mob AI, life, deaths"]
        HH["Own hero"]
        HR["RemoteHero per client<br/>replays its input"]
    end
    subgraph Client["Client"]
        CW["Puppet mobs: display only"]
        CH["Own hero"]
        CR["RemoteHero per other player<br/>shows their hero"]
    end
    HW -- "mob state 15 Hz, animations, deaths, hits" --> CW
    HH -- "input and state 30 Hz" --> CR
    CH -- "input and state 30 Hz" --> HR
```

- **The same levels:** every game builds each level from the same seed, and a signature over its mobs and items proves they match.
- **The second hero:** `RemoteHero` extends the game's own `Beheaded` and is driven by a virtual gamepad, so weapons and skills run the original code.
- **Mob targets:** mob code reads `game.hero` in about 500 places. `HeroFocus` swaps in the nearest player while a mob runs.
- **Joining mid-run:** what a level holds depends on the way there, so a new player walks the host's route level by level, then gets the mobs already dead and the items already moved. A continued save gives its route from the save itself, and the level the host left is trimmed to match.
- **More players:** everyone links to the host, which passes on what each player says about their own hero to the others.
- **Network:** Steam's `ISteamNetworkingSockets`, with Steam's relays when P2P fails. Rooms are Steam lobbies.

| Path | Contents |
|---|---|
| [`mod/RingoMP`](mod/RingoMP) | The mod: heroes, lobby, main menu, translations |
| [`mod/RingoMP/Net`](mod/RingoMP/Net) | Networking: sync, gathering, rescue, items, progress, transport |
| [`mod/RingoMP/Dev`](mod/RingoMP/Dev) | Automated test scenarios |
| [`tools`](tools) | Test scripts and bytecode tools |
| [`crates`](crates) | An early Rust loader prototype, not used by the current version |

## Building from source

You need Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download) and an installed DCCM.

```powershell
$env:DCCM_MDK_ROOT = "D:\your path\Dead Cells\coremod\core\mdk"
dotnet nuget add source "$env:DCCM_MDK_ROOT\packages" --name DeadCoreModdingMDK
dotnet build mod\RingoMP\RingoMP.csproj
```

The build installs the mod into `coremod\mods` by itself.

The test scripts start two games (or more) on a hidden Windows desktop: no windows, no stolen focus, and no writes to your save. Steam must be running.

```powershell
$env:DEADCELLS_DIR = "D:\your path\Dead Cells"
tools\net.ps1 240 gather   # door gathering and several levels in a row
tools\net.ps1 240 team     # going down, rescue, shared items
tools\net.ps1 240 rooms    # going into a side room together and back out
tools\net.ps1 240 modes    # the daily challenge, then Boss Rush
tools\crowd.ps1 4          # four players: everyone together, one door for all, one leaving
tools\join.ps1             # joining mid-run and coming back after a crash
tools\lobby.ps1            # rooms and lobby, with pictures in build\
tools\pause.ps1            # the lobby from the pause menu, with pictures
python -I tools\fontcheck.py "$env:DEADCELLS_DIR\res.pak" mod\RingoMP\Translations.cs
```

**Translations** live in [`Translations.cs`](mod/RingoMP/Translations.cs), one column per game language. `tools\fontcheck.py` checks each one against that language's game font, since a letter the font lacks would simply not show. Corrections from native speakers are very welcome.

## Credits

- [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core): the modding framework that makes this possible
- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET): Steam bindings
- [Motion Twin](https://motion-twin.com/) and [Evil Empire](https://evilempire.games/), for such a great game

This project was written from scratch and uses no code from other multiplayer mods.

## Disclaimer

This is an unofficial fan project, not affiliated with Motion Twin, Evil Empire or Playdigious. The repository contains no game code or assets, and you need your own copy of the game. The mod is provided as is; back up your save before use. See [LICENSE](LICENSE).
