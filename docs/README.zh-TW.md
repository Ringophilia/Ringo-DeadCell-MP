<div align="center">

# Ringo Dead Cells Multiplayer

**為《死亡細胞》加入雙人連線：一起闖關，一起打 Boss，一起進門。**

[English](../README.md) · [简体中文](README.zh-CN.md) · **繁體中文** · [日本語](README.ja.md) · [Русский](README.ru.md) · [Español](README.es.md)

</div>

這是快速指南。完整說明、運作原理與建置方式請看 [English README](../README.md)。

> [!WARNING]
> **早期預覽版。** 所有功能都通過了單機雙開的自動化測試，但**還沒有在兩個真實 Steam 帳號之間連線過**。可能會當機、連不上或不同步，開始前請備份存檔。

## 功能

- 兩人在同一局各自操控角色，怪物、死亡、掉落保持同步
- 主選單的 **多人遊戲** 進入大廳：瀏覽好友與公開房間，建立、加入或管理房間
- 按門後在門口等待，可隨時取消；兩人到同一扇門才一起前往下一關
- 倒下後隊友有 30 秒可以救起你，等待時鏡頭跟著隊友
- 隊友頭上顯示名字；你也解鎖了隊友的服裝時，隊友會穿著自己的服裝
- 怪物血量依人數提高；地上的裝備共用，卷軸、細胞和金幣各拿各的
- 依兩人中較低的進度產生關卡，不會改寫存檔裡已有的解鎖
- 隨時加入、斷線重連、當機後帶著裝備回到原關卡
- 大廳與所有提示跟隨遊戲語言（13 種）

## 安裝

1. 兩位玩家都需要 Steam 正版《死亡細胞》v35 與 [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core) 35.14.2。
2. 從 [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases) 下載 `RingoMP-v0.2.0.zip`，把 `RingoMP` 資料夾放進遊戲目錄的 `coremod\mods\`。
3. 備份存檔：`Steam\userdata\<你的ID>\588650\remote`。
4. 執行 `coremod\core\host\startup\DeadCellsModding.exe` 啟動遊戲。

## 怎麼玩

1. 主選單選 **多人遊戲**。
2. 按 **`N`** 建立房間、**`I`** 呼叫好友，或按數字鍵加入列表中的房間。
3. 好友加入後，房主按 **`S`** 開始新遊戲或 **`C`** 繼續存檔，好友會自動跟上。

其他按鍵：`R` 重新整理、`K` 移出玩家、`L` 鎖定、`P` 切換可見範圍、`X` 關閉或退出房間、`Esc` 返回、`F5` 隨時開關大廳。

## 已知限制

- 最多 2 人，只支援主線，不含每日挑戰與 Boss Rush。
- 回來的玩家會恢復進入當前關卡時的物品與金幣，但不含武器附加詞條。
- 銀行與部分子房間尚未適配。

問題請到 [Issues](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues) 回報，並附上 `coremod\logs` 裡最新的記錄檔。常見問題見 [Troubleshooting](TROUBLESHOOTING.md)。
