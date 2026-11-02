<div align="center">

# Ringo Dead Cells Multiplayer

**给《死亡细胞》加上双人联机：一起闯关，一起打 Boss，一起进门。**

从零编写的开源联机 Mod，基于 [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core)。

[![Release](https://img.shields.io/github/v/release/Ringophilia/Ringo-DeadCell-MP?include_prereleases&label=release)](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Game](https://img.shields.io/badge/Dead%20Cells-v35-c0392b)
![Players](https://img.shields.io/badge/players-2-27ae60)
![Status](https://img.shields.io/badge/status-early%20preview-orange)

[功能](#功能) · [安装](#安装) · [怎么玩](#怎么玩) · [联机规则](#联机规则) · [项目状态](#项目状态) · [工作原理](#工作原理) · [从源码构建](#从源码构建) · [English](#english)

</div>

> [!WARNING]
> **这是早期预览版。** 所有功能都通过了本机双开的自动化测试，但**还没有在两个真实 Steam 账号之间联机过**，也没有人完整打通过一局。可能遇到崩溃、连不上或不同步。开始前请备份存档，详见[项目状态](#项目状态)。

## 功能

| | |
|---|---|
| 🎮 **双人同屏世界** | 两人在同一局里各控制一个角色，怪物、死亡、掉落都保持同步 |
| 🏠 **房间大厅** | 主菜单选“多人游戏”进入大厅，查看好友和公开房间，新建、加入或管理房间 |
| 👑 **房主管理** | 查看成员、邀请好友、移出玩家、锁定房间、切换公开或好友可见 |
| 🚪 **进门集合** | 按门后在门口等待，可随时取消；两人到同一扇门才一起进入下一关 |
| 🛟 **倒地救援** | 血量耗尽先倒地，队友 30 秒内可以救起；没救到下一关自动复活，全灭才结束 |
| ⚖️ **双人平衡** | 怪物血量按人数提高，地面装备共享，卷轴、细胞和金币各拿各的 |
| 📈 **进度保护** | 按两人中较低的进度生成关卡；不改写存档里已有的解锁，局内新解锁照常保存 |
| 🔌 **随时加入与重连** | 主机可以先玩，朋友中途加入；断线自动重连，崩溃后回到原关并恢复装备 |

## 安装

**两位玩家都需要：**

1. 正版《死亡细胞》PC 版（Steam），已在 v35 上测试。
2. 安装 [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core)（已在 35.14.2 上测试）。
3. 从 [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases) 下载 `RingoMP-v0.1.0.zip`。
4. 把解压出的 `RingoMP` 文件夹放进游戏目录下的 `coremod\mods\`：

```text
Dead Cells\
└─ coremod\
   └─ mods\
      └─ RingoMP\
         ├─ RingoMP.dll
         └─ modinfo.json
```

5. 备份存档：`Steam\userdata\<你的ID>\588650\remote`。
6. 运行 `coremod\core\host\startup\DeadCellsModding.exe` 启动游戏。

> [!TIP]
> 想让朋友**在游戏没打开时**也能直接接受邀请，请在 Steam 的游戏属性里把启动选项设为下面这行（路径换成你自己的）。这项还没有实测：
>
> ```text
> "D:\你的路径\Dead Cells\coremod\core\host\startup\DeadCellsModding.exe" %command%
> ```

## 怎么玩

1. 主菜单选择 **多人游戏**，进入大厅。
2. 按 **`N`** 新建房间，再按 **`I`** 邀请好友；或者按数字键加入列表里的房间。
3. 朋友加入后，房主按 **`S`** 开始新游戏，或按 **`C`** 继续存档，朋友会自动跟上。

| 按键 | 大厅 | 房间内 |
|---|---|---|
| `N` / `R` | 新建房间 / 刷新 | |
| `1`–`9` | 加入对应房间 | |
| `S` / `C` | | 开始新游戏 / 继续存档（房主） |
| `I` | | 邀请好友 |
| `K` | | 移出玩家（房主） |
| `L` | | 锁定或解锁（房主） |
| `P` | 切换新房间可见范围 | 切换可见范围（房主） |
| `X` | | 关闭房间（房主）/ 退出房间 |
| `Esc` | 返回 | 返回 |
| `F5` | 随时打开或隐藏大厅，局内也可以用 | |

## 联机规则

**过关**

- 按门后进入等待，再按一次或走开 3 格就取消，误按没有代价。
- 两人在不同的门等待时都不会走，走到同一扇门才一起出发。
- 掉落、隐藏通道这类不走门的路线，一个人走后另一个人会自动跟上。

**死亡**

- 倒地后，队友靠近并按住交互键 1.5 秒救起，恢复一半血量。
- 30 秒内没救起，下一关自动复活。
- 只有两人都倒下，这一局才结束。

**资源**

| 资源 | 规则 |
|---|---|
| 卷轴、细胞、金币 | 每人一份，各拿各的 |
| 地面的武器、技能、护符 | 共享，一人拿走另一边消失；换下的装备会留在地上给队友 |
| 开局军械库 | 各选各的 |
| 怪物掉落 | 每人一份 |

**怪物强度**（起始值，会根据实测调整）

| 类型 | 血量 | 伤害 |
|---|---|---|
| 普通怪 | ×1.5 | 不变 |
| 精英 | ×1.7 | 不变 |
| Boss | ×1.8 | 不变 |

**进度**

- 一局按两人中较低的进度来：取解锁的交集和较少的 Boss 细胞，这样两边才能生成相同的关卡。
- 这只影响读取，不改写存档里已有的解锁；局内新解锁的内容走游戏原本的保存流程，存进各自的存档。

## 项目状态

**已通过自动化测试**（本机双开，部分带 5% 丢包和 80ms 延迟）

- [x] 角色、怪物、动画、受击同步
- [x] 进门集合、取消、不同门分流、连续过关
- [x] 倒地、救援、超时复活、全灭后开新局
- [x] 共享装备、换下的装备
- [x] 按最低进度生成相同关卡（在一份存档上模拟）
- [x] 断线重连、对方离开后继续单人
- [x] 中途加入、崩溃后回来并恢复装备
- [x] 真实 Steam 上创建房间、列出房间

**尚未验证**

- [ ] 两个真实 Steam 账号之间的 P2P 连接和邀请
- [ ] 真人完整打通一局
- [ ] 大厅界面的实际显示和按键
- [ ] 联机时写入并读回真实存档
- [ ] Giant、Queen、Dooku、Collector 的正常入场战斗

**已知限制**

- 目前最多 2 人，只支持主线，不含每日挑战和 Boss Rush。
- 房主崩溃后，房主这边无法恢复联机，客户端继续单人。
- 主机单人开局后朋友才加入时，当前关怪物血量不加倍，下一关才生效。
- 主机继续存档时，朋友要等主机进入下一关才能汇合。
- 回来的玩家恢复的是进入当前关时的物品和金币，武器附加词条不恢复。
- 银行和部分子房间的过关方式还没有适配。

欢迎提 [Issue](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues)，请附上 `coremod\logs` 里最新的日志。

## 工作原理

《死亡细胞》全局有约 15600 处不带种子的随机调用，关卡生成还依赖存档内容，所以不能用帧同步。这个 Mod 的做法是**主机拥有世界，每位玩家拥有自己的角色**：

```mermaid
flowchart LR
    subgraph Host["主机"]
        HW["世界：怪物 AI、生命、死亡"]
        HH["自己的角色"]
        HR["RemoteHero<br/>重放客户端的输入"]
    end
    subgraph Client["客户端"]
        CW["木偶怪物：只显示"]
        CH["自己的角色"]
        CR["RemoteHero<br/>显示主机角色"]
    end
    HW -- "怪物状态 15Hz、动画、死亡、命中" --> CW
    HH -- "输入和状态 30Hz" --> CR
    CH -- "输入和状态 30Hz" --> HR
```

- **相同的关卡：** 两边用同一个种子生成，并用怪物和物品算出的签名确认一致。
- **第二个角色：** `RemoteHero` 继承游戏自己的 `Beheaded`，用虚拟手柄驱动，所以武器和技能都走原版逻辑。
- **怪物的目标：** 怪物代码在约 500 处读取 `game.hero`，`HeroFocus` 在怪物运行时把它临时换成最近的玩家。
- **中途加入：** 关卡内容和走过的路线有关，所以新玩家会按主机的路线逐关走到当前关，再补上已死的怪和被动过的物品。
- **网络：** 使用 Steam 的 `ISteamNetworkingSockets`，P2P 失败时走 Steam 中继；房间就是 Steam 大厅。

| 路径 | 内容 |
|---|---|
| [`mod/RingoMP`](mod/RingoMP) | Mod 本体：角色、大厅、主菜单 |
| [`mod/RingoMP/Net`](mod/RingoMP/Net) | 网络层：同步、集合、救援、物品、进度、传输 |
| [`mod/RingoMP/Dev`](mod/RingoMP/Dev) | 自动化测试场景 |
| [`tools`](tools) | 测试脚本和字节码分析工具 |
| [`crates`](crates) | 早期的 Rust 加载器原型，当前版本不使用 |

## 从源码构建

需要 Windows、[.NET SDK 10](https://dotnet.microsoft.com/download)，以及已安装的 DCCM。

```powershell
$env:DCCM_MDK_ROOT = "D:\你的路径\Dead Cells\coremod\core\mdk"
dotnet nuget add source "$env:DCCM_MDK_ROOT\packages" --name DeadCoreModdingMDK
dotnet build mod\RingoMP\RingoMP.csproj
```

构建完成后，Mod 会自动装进 `coremod\mods`。

测试脚本会在一个隐藏的 Windows 桌面上启动两个游戏，不弹窗、不抢焦点，也不会写入存档：

```powershell
$env:DEADCELLS_DIR = "D:\你的路径\Dead Cells"
tools\net.ps1 240 gather   # 进门集合与连续过关
tools\net.ps1 240 team     # 倒地、救援、共享物品
tools\join.ps1             # 中途加入与崩溃回归
tools\lobby.ps1            # 房间和大厅
```

## 致谢

- [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core)：让这一切成为可能的 Mod 框架
- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET)：Steam 接口绑定
- [Motion Twin](https://motion-twin.com/) 和 [Evil Empire](https://evilempire.games/)：做出了这么好的游戏

本项目从零编写，没有使用其他多人 Mod 的代码。

## 免责声明

这是非官方的粉丝项目，与 Motion Twin、Evil Empire 和 Playdigious 没有关联。仓库不包含任何游戏代码或资源，你需要自己拥有正版游戏。Mod 按“现状”提供，使用前请备份存档，详见 [LICENSE](LICENSE)。

---

## English

**Ringo Dead Cells Multiplayer** is an open-source, two-player co-op mod for Dead Cells, written from scratch on top of [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core).

> [!WARNING]
> **Early preview.** Everything passes automated two-game tests on one machine, but it has **never been played between two real Steam accounts**, and nobody has finished a full run with it yet. Back up your save first.

**Features:** a room lobby in the main menu with host controls, door gathering you can cancel at any time, downed state and rescue, shared equipment with personal scrolls, cells and gold, mob life scaling for two players, a session at the lower player's progress that never rewrites saved unlocks, joining mid-run, and reconnecting after a dropped link or a crash.

**Install:** both players need Dead Cells v35 on Steam and DCCM 35.14.2. Download `RingoMP-v0.1.0.zip` from [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases), put the `RingoMP` folder into `coremod\mods\`, and start the game with `coremod\core\host\startup\DeadCellsModding.exe`.

**Play:** choose **Multiplayer** in the main menu, press `N` to create a room and `I` to invite a friend, or press a number to join a listed room. The host starts with `S` or continues a save with `C`. `F5` opens the lobby at any time.

Released under the [MIT License](LICENSE). This is an unofficial fan project, not affiliated with Motion Twin, Evil Empire or Playdigious, and it contains no game code or assets.
