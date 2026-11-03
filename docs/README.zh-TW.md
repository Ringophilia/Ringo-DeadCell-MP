<div align="center">

# Ringo Dead Cells Multiplayer

**為《死亡細胞》加入最多四人的連線：一起闖關，一起打 Boss，一起進門。**

[English](../README.md) · [简体中文](README.zh-CN.md) · **繁體中文** · [日本語](README.ja.md) · [Русский](README.ru.md) · [Español](README.es.md)

</div>

這是快速指南。完整說明、運作原理與建置方式請看 [English README](../README.md)。

> [!WARNING]
> **早期預覽版。** 所有功能都通過了單機雙開的自動化測試，但**還沒有在兩個真實 Steam 帳號之間連線過**。可能會當機、連不上或不同步，開始前請備份存檔。

## 功能

- 每位玩家在同一局各自操控角色，怪物、死亡、掉落保持同步
- 主選單的 **多人遊戲** 進入大廳：瀏覽好友與公開房間，建立、加入或管理房間
- 按門後在門口等待，可隨時取消；所有人到同一扇門才一起前往下一關
- 倒下後其他隊友有 30 秒可以救起你，等待時鏡頭跟著一位隊友
- 每位隊友頭上顯示名字；你也解鎖了他們的服裝時，他們會穿著自己的服裝
- 每多一位玩家，怪物血量就再提高一檔；地上的裝備共用，卷軸、細胞和金幣各拿各的
- 依所有玩家中最低的進度產生關卡，不會改寫存檔裡已有的解鎖
- 每日挑戰與 Boss Rush：房主開始後所有人一起跟上，一起玩的這一局不會把分數送上排行榜
- 隨時加入、斷線重連、當機後帶著裝備回到原關卡
- 大廳與所有提示跟隨遊戲語言（13 種）

## 安裝

1. 每位玩家都需要 Steam 正版《死亡細胞》v35 與 [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core) 35.14.2。
2. 從 [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases) 下載 `RingoMP-v0.4.0.zip`，把 `RingoMP` 資料夾放進遊戲目錄的 `coremod\mods\`。
3. 備份存檔：`Steam\userdata\<你的ID>\588650\remote`。
4. 執行 `coremod\core\host\startup\DeadCellsModding.exe` 啟動遊戲。

## 怎麼玩

1. 主選單選 **多人遊戲**。
2. 選擇 **建立房間**，再選 **呼叫好友**；或直接選列表中的房間加入。
3. 好友加入後，房主選擇 **開始新遊戲**、**繼續存檔** 或 **開始每日挑戰**，所有人會自動跟上。Boss Rush 從遊戲裡它的門進入，同樣所有人一起。

大廳和遊戲的設定選單是同一種介面：可以用滑鼠點選，也可以用鍵盤或手把移動、確認。局內的暫停選單也有 **多人遊戲**，打開的是同一個大廳。進出子房間（挑戰房、銀行等）的門和過關的門一樣，大家一起進出。

## 已知限制

- 最多 4 人。
- 不支援自訂模式。

問題請到 [Issues](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues) 回報，並附上 `coremod\logs` 裡最新的記錄檔。常見問題見 [Troubleshooting](TROUBLESHOOTING.md)。
