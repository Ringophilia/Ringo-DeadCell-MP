# 常见问题

[English](TROUBLESHOOTING.md) · **简体中文** · [返回说明](README.zh-CN.md)

## 先确认这两点

- 两位玩家必须使用**同一版本**的 RingoMP。版本不同时可能连得上，但很快就会不同步。
- 最新的日志在 `Dead Cells\coremod\logs`，本 Mod 的日志行以 `RINGO` 开头。提 [Issue](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues) 时请附上这个文件。

## 游戏启动后马上关闭

- 启动 `DeadCellsModding.exe` 之前**必须先打开 Steam**。日志里出现 `Steam init failed` 就是这个原因。
- 游戏目录里的 DCCM 文件夹必须正好叫 `coremod`。
- 先确认不带 RingoMP 时 DCCM 能正常启动：把 `coremod\mods\RingoMP` 移走再试。如果还是关闭，问题出在 DCCM 的安装上。

## 主菜单里没有“多人游戏”

- Mod 没有加载。文件路径应该是 `coremod\mods\RingoMP\RingoMP.dll` 和 `coremod\mods\RingoMP\modinfo.json`，不要多套一层文件夹。
- 请用 `coremod\core\host\startup\DeadCellsModding.exe` 启动，而不是 Steam 的“开始游戏”按钮，除非你按说明设置了启动选项。
- 日志里应该能看到 `Loading mod: RingoMP`。如果紧接着出现报错，请带上日志提 Issue。

## 朋友看不到我的房间

- 新房间默认**仅好友可见**，你们需要是 Steam 好友。想让所有人都能找到，可以在创建前按 `P`，或者以房主身份在房间里按 `P`。
- 在房间列表按 `R` 刷新。好友的房间要等 Steam 告诉游戏好友在玩什么才会出现，可能需要几秒。
- 满员或锁定的房间仍会显示，但无法加入。房主按 `L` 解锁。
- 按 `I` 邀请会打开 Steam 界面。如果没有反应，请确认 Steam 界面（Overlay）对《死亡细胞》是开启的。

## 在同一个房间里却一直连不上

- 连接走 Steam，直连失败时会自动改走 Steam 中继，不需要开放端口。
- 连接建立之前，房间会显示“正在连接房主...”。如果一分钟以上都没变化，请两人都退出房间，由房主重新创建。
- VPN 或屏蔽 Steam 网络的防火墙可能会阻止连接，请关掉 VPN 再试。

## 按 S 或 C 没有反应

- 只有房主能开始游戏，而且只能在标题画面。
- 刚加入时双方要先交换进度，“正在与队友同步进度”的提示过一会儿就会消失。

## 朋友加入后自己在一关一关地走

- 这是正常的。关卡内容和走过的路线有关，所以中途加入的玩家会快速走一遍房主走过的关卡，直到追上房主。
- 房主继续存档后，朋友会直接进入房主所在的关卡，房主已经打死的怪在朋友这边也会消失。如果关卡仍然对不上，会提示“等待房主进入下一关后汇合”，到下一扇门再会合。

## 名字里出现问号

- 名字里有游戏字体没有的字时，会显示成 `?`。游戏自带的字体只包含游戏本身需要的字。

## 卸载

- 删除 `coremod\mods\RingoMP` 即可。Mod 不会改写存档里已有的内容；如果出了问题，请用之前从 `Steam\userdata\<你的ID>\588650\remote` 备份的文件恢复。
