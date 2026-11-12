# C 槽清理助手

免安裝的 Windows 清理工具，先預覽、再勾選，最後要求 Windows 將檔案移到資源回收筒。

## 功能

- 按「掃描」才開始，啟動不自動掃描、不自動清理。
- 固定掃描 C 槽的使用者 `AppData\Local\Temp`、`AppData\Local\CrashDumps`（僅 `.dmp`）及 `Windows\Temp`。位於其他磁碟的設定檔不納入。
- 保留最近 7／14／30 天建立或修改的檔案，預設 7 天。列出類別、邏輯大小、修改時間與完整路徑，預設全部不勾選。
- 跳過連結／junction、唯讀、系統及離線檔案。無存取權限會跳過，不要求提升權限。
- 回收前再次檢查固定範圍、建立／修改時間、大小及獨占開啟能力。變動或被使用的檔案跳過，不強制解除唯讀、不刪除資料夾。
- 可停止掃描及後續回收，正在顯示的 Windows 對話框需由使用者處理。
- 提供 Windows 儲存空間、系統磁碟清理、資源回收筒入口。

## 使用方式

1. 開啟 [`DiskCleanupHelper.exe`](../../release/DiskCleanupHelper.exe)，選擇保留天數並掃描。
2. 檢視完整路徑，勾選不需要的候選檔案。暫存資料夾也可能被軟體用來放工作資料，候選不代表絕對無用；先儲存工作並關閉其他應用程式。
3. 按「回收勾選項目」，確認數量與大小。若 Windows 提示無法回收、要永久刪除，請取消；工具保留 Windows 的永久刪除警告，不自動回覆該警告。
4. 下方會顯示處理結果。移入回收筒仍占用同磁碟空間；確認不需還原後再自行清空。本工具不清空回收筒，也不將處理檔案大小當成實際釋放空間。

顯示的檔案大小為邏輯大小，可能與實際占用空間不同。無權限、路徑過長等項目會略過。回收前的檢查無法完全排除其他程式在最後瞬間改動檔案，因此建議先關閉其他應用程式。

## 舊 BAT 清理規則取捨

| 舊規則 | 本工具處理方式 |
| --- | --- |
| 全磁碟刪除 `*.tmp`、`*._mp` | 只納入固定 Temp 目錄，套用保留天數，不依副檔名搜尋全磁碟 |
| 全磁碟刪除 `.log`、`.gid`、`.chk`、`.old` | 不加入，可能是應用程式資料、記錄或復原資料 |
| Windows 目錄的 `.bak` | 不加入，避免移除備份與還原資料 |
| 刪除整個 Windows Temp 再重建 | 保留目錄與權限，逐檔處理 |
| Prefetch | 不加入一般清理 |
| Cookies、Recent | 不加入空間清理，涉及登入狀態與使用紀錄 |
| recycled、Temporary Internet Files | 改提供 Windows 內建清理入口，不操作舊相容路徑 |

Windows 更新清理、舊版 Windows、瀏覽器快取由使用者在內建工具確認；不直接處理 WinSxS、Installer、SoftwareDistribution、系統還原、休眠檔或分頁檔。

## 系統需求

- Windows 10／11，.NET Framework 4.5 或更新版本（Windows 10／11 內建）。
- 一般使用者權限，無第三方 runtime、無網路需求。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-disk-cleanup-helper.ps1
```

成品：`release/DiskCleanupHelper.exe`，包含專屬圖示。圖示由 `build-disk-cleanup-helper-icon.ps1` 產生，保留於 `assets/icons/`。

```powershell
powershell -ExecutionPolicy Bypass -File .\test-disk-cleanup-helper.ps1
```

測試僅掃描 `.build/` 中新建的測試資料，涵蓋保留天數、連結跳過、越界及變動拒絕、鎖定檔案、取消與視窗開啟。Windows 回收測試僅處理專案內一個新建的測試檔，不清空資源回收筒。

參考：[Windows 儲存空間管理](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/free-up-drive-space-in-windows)、[Shell 檔案操作旗標](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags)。
