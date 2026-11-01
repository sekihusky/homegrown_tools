# 影片剪輯

## 功能

選擇 MP4 和起訖時間，保留指定片段，輸出至原影片資料夾。原片不會被覆蓋，若檔案名稱已存在則自動加上編號。影片會重新編碼為 H.264，音訊編碼為 AAC。

## 使用方式

開啟 `release/VideoTrimmer/VideoTrimmer.exe`，選擇影片並以 `時:分:秒` 輸入開始及結束時間，按「開始剪輯」。請等待完成訊息。

可使用內建播放器預覽及拖曳進度，在想要的位置按「設為開始」或「設為結束」。時間以秒為單位，也可手動輸入。預覽使用 Windows 媒體播放元件，支援的格式取決於系統的解碼器。

## 系統需求

Windows 10/11 64 位元、.NET Framework 4.5 以上；請複製完整的 `VideoTrimmer` 資料夾使用。輸出磁碟需有足夠空間。

## 重新建置

雙擊專案根目錄的 `build-video-trimmer.cmd`，或執行 `powershell -ExecutionPolicy Bypass -File .\build-video-trimmer.ps1`。首次建置需要網路下載 FFmpeg，並透過既有的影片壓縮工具建置腳本驗證下載檔。

FFmpeg 為 GPLv3 第三方元件；對外散布時需遵守其授權，詳見成品資料夾中的授權文件。
