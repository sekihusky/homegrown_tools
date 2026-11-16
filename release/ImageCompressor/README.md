# 網站圖片減肥

## 功能

- 遞迴處理 JPG／JPEG、PNG、WebP，保留寬高、檔名、格式與路徑，不修改其他檔案。
- 預設無損壓縮；可允許 JPG／WebP 畫質損失並設定品質（預設 85）。PNG 始終無損。
- 只替換確實變小且通過驗證的圖片；無損模式比較解碼像素，同時檢查圖片方向、透明屬性及原有描述資料。
- 跳過動畫、多頁、超過 8 位元深度、格式與副檔名不符的圖片。GIF、SVG、AVIF 不在本版範圍。
- 排除備份目錄及檔案／資料夾連結，顯示無法讀取的目錄。
- 可停止後續處理；已完成圖片及備份保留。
- 完全在本機執行，不上傳圖片，使用時不需要網路。

## 使用方式

1. 複製整個 `release/ImageCompressor/`，開啟 `ImageCompressor.exe`。
2. 按「選取資料夾」選擇網站根目錄。
3. 預設無損；需要更小的 JPG／WebP 可勾選允許畫質損失。
4. 按「開始批次壓縮」，查看個別處理結果與節省容量。
5. 按「開啟本次備份」查看原圖與 CSV 報告。

備份建立在所選資料夾內，保留原有相對路徑，例如：

```text
D:\網站\images\banner.jpg
D:\網站\_圖片壓縮備份\20261116_153000_000_a1b2c3\images\banner.jpg
```

每次執行建立獨立目錄，只備份實際替換的原圖。沒有變小的圖片保留原檔，不另備份。還原時先關閉工具，再把本次備份內的圖片按原有相對路徑複製回網站目錄；不要複製「處理報告.csv」。

工具直接替換所選資料夾內的圖片。處理期間請避免同時修改原圖。使用 Windows 原子替換，同時產生原圖備份；檔案系統不支援時顯示失敗並保留原檔。壓縮量取決於原圖，已最佳化的圖片可能無法再縮小。允許畫質損失時，反覆執行可能使畫質逐次下降，建議只處理一次。

壓縮完成後，需自行上傳處理過的圖片到網站；不要將備份目錄上傳。

## 系統需求

- Windows 10／11，64 位元。
- .NET Framework 4.8（新版 Windows 10／Windows 11 內建）。
- 免安裝，EXE 與隨附 DLL 必須放在一起。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-image-compressor.ps1
```

首次建置從 NuGet 下載 Magick.NET-Q8-x64／Magick.NET.Core 15.1.0 及 Roslyn 編譯器 4.14.0，快取於 `packages/image-compressor/`。成品放在 `release/ImageCompressor/`，執行時不需要編譯器。

驗證：執行 `tests/image-compressor/run.ps1`，涵蓋尺寸、透明背景、無損及有損模式、原圖備份、非圖片檔案不變、動畫略過與停止處理。

壓縮引擎：[Magick.NET](https://github.com/dlemstra/Magick.NET)，Apache-2.0 授權。第三方授權隨成品發行。
