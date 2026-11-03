# Troubleshooting

**English** · [简体中文](TROUBLESHOOTING.zh-CN.md) · [Back to the README](../README.md)

## Before anything else

- Every player needs **the same version** of RingoMP. A different version may connect and then fall out of step.
- The newest log is in `Dead Cells\coremod\logs`. Lines from this mod start with `RINGO`. Please attach that file to any [issue](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues) you open.

## The game closes right after starting

- **Steam must be running** before you start `DeadCellsModding.exe`. If the log says `Steam init failed`, start Steam and try again.
- The DCCM folder in the game folder must be called exactly `coremod`.
- Check that DCCM itself starts without RingoMP: move `coremod\mods\RingoMP` out and start again. If it still closes, the problem is in the DCCM install.

## There is no Multiplayer entry in the main menu

- The mod is not loaded. The files must be `coremod\mods\RingoMP\RingoMP.dll` and `coremod\mods\RingoMP\modinfo.json`, not one folder deeper.
- Start the game with `coremod\core\host\startup\DeadCellsModding.exe`, not with Steam's Play button, unless you set the launch option shown in the README.
- The log should contain `Loading mod: RingoMP`. If it shows an error right after, open an issue with the log.

## My friend cannot see my room

- New rooms are **friends only**, and you have to be Steam friends. To make a room anyone can find, press `P` before creating it, or in the room as its owner.
- Press `R` in the room list to refresh. Friends' rooms show up once Steam has told the game what your friends are playing, which can take a few seconds.
- A full or locked room still shows, but nobody can join it. The owner unlocks it with `L`.
- Inviting with `I` opens the Steam overlay. If nothing shows, check that the Steam overlay is enabled for Dead Cells.

## We are in the same room but never connect

- The link goes through Steam, with Steam's relays when a direct connection fails, so no ports need to be opened.
- The room shows "Connecting to the host..." until the link is up. If it stays there for more than a minute, everyone should leave the room and the owner should create a new one.
- A VPN or a strict firewall that blocks Steam's networking can stop the link. Try without the VPN.

## Pressing S or C does nothing

- Only the room's owner starts runs, and only at the title screen.
- Right after joining, the games exchange their progress first. The message "Still syncing with your partner" goes away after a moment.

## My friend joined and is walking through levels on their own

- That is expected. What a level holds depends on the way there, so a player joining a run goes through the host's levels one by one, quickly, until they reach the host.
- After the host continues a save, the friend arrives in the host's level, with the mobs the host already killed gone there too. If the levels still differ, the message "You will meet the host in their next level" appears, and you meet at the next door.

## Names show question marks

- A name with letters that the game's font for your language does not have shows them as `?`. The game's own fonts only hold the letters the game needs.

## Uninstalling

- Delete `coremod\mods\RingoMP`. The mod never rewrites what is already in your save, but if anything went wrong, restore the backup you made from `Steam\userdata\<your id>\588650\remote`.
