# 影片減肥 · MP4 壓縮工具

## 功能

- 單支 MP4 重新編碼；畫質優先、平衡、容量優先三種設定。
- H.265（預設）或 H.264（播放相容性優先），採 CPU 編碼，不需要顯示卡。
- 保留解析度，不設定新的影格率；保留所有音訊軌道，不重新壓縮音訊。
- 先試壓前 15 秒，播放後確認觀感，再壓整支影片。
- 顯示進度、壓縮前後容量與縮小比例；可取消並清理未完成檔案。
- 原始影片不覆蓋；輸出檔已存在時會拒絕覆蓋。

## 使用方式

1. 開啟 `release/VideoCompressor/VideoCompressor.exe`。
2. 選取 MP4，確認新檔輸出位置。
3. 建議先選「畫質優先」及 H.265，按「試壓前 15 秒」。
4. 按「開啟輸出位置」，使用自己的播放器比較原檔與試壓片段。
5. 確認後按「開始壓縮」。若需要較廣的播放支援，選 H.264。

試壓取影片前 15 秒，不代表整支影片的複雜度；其容量不可直接與原始完整影片比較。
壓縮為有損編碼，不能保證肉眼完全無差異，亦不能保證一定變小。輸出變大時會明確提示。
第一版暫不支援 HDR（PQ / HLG），以免不正確處理亮度及色彩。僅輸出第一條影像與所有音訊，不保留字幕、其他影像、資料軌道或特殊 Dolby Vision 資訊。
H.264 模式會使用 8-bit yuv420p，較高位深／色度取樣的來源可能有額外轉換。

## 系統需求

- Windows 10 / 11，64 位元；.NET Framework 4.5 或以上。
- FFmpeg 與 FFprobe 已放在程式旁，使用者不需安裝或設定 PATH。
- 磁碟需有足夠空間容納新檔。H.265 CPU 壓縮可能耗時較長。
- 請複製整個 `VideoCompressor` 資料夾；不要只複製主程式。

## 重新建置

在專案根目錄執行 `powershell -ExecutionPolicy Bypass -File .\build-video-compressor.ps1`。
從 GitHub 下載或 clone 的專案未包含 ffmpeg.exe 與 ffprobe.exe，請先執行上述建置腳本，再使用成品。完整的本機發行資料夾仍為免安裝版。第一次建置需要網路下載 FFmpeg；快取位於 `packages/video-compressor`，下載檔會檢查 SHA256。

若不方便手動輸入 PowerShell 指令，也可以在專案根目錄雙擊 `build-video-compressor.cmd`。建置完成後，請複製整個 `release/VideoCompressor/` 資料夾；其中包含主程式、`ffmpeg.exe` 與 `ffprobe.exe`。

設定：H.265 CRF 20 / 23 / 26；H.264 CRF 18 / 21 / 24；preset medium。

## 第三方元件

FFmpeg Windows essentials build 來自 https://www.gyan.dev/ffmpeg/builds/ （FFmpeg 官方下載頁 https://ffmpeg.org/download.html 列出此供應者）。
保留下載包的授權與 README，見同目錄 `FFmpeg-LICENSE.txt`、`FFmpeg-README.txt`，其中包含版本與上游資訊。
此 FFmpeg build 為 GPLv3。若對外重新散布，需提供該版本與相依函式庫的對應原始碼及適用授權資料；單純附上網址不代表已滿足全部散布義務。此工具透過獨立程序呼叫 FFmpeg。

## 驗證

執行 `powershell -ExecutionPolicy Bypass -File .\tests\video-compressor\run-smoke.ps1`，會產生合成影片並檢查兩種編碼、解析度、片長、音訊、試壓、取消清理、HDR 拒絕、原檔 SHA256 與完整解碼。測試素材存於 .build，不使用你的影片。
