# 全球網址回應測試

## 功能

免安裝 Windows 桌面工具，透過 Globalping 實際遠端節點測量 22 個主要城市連線 HTTP／HTTPS 網址的狀態。顯示實際節點與網路、HTTP 狀態碼、總耗時、DNS／TCP／TLS／首位元組時間（毫秒）與目標 IP。

明確標示連線失敗、逾時、DNS 失敗、HTTP 錯誤、重新導向、無可用節點、節點離線、服務額度限制及服務等待逾時。支援取消與含診斷的 UTF-8 CSV 匯出。

## 使用方式

1. 執行 [GlobalUrlChecker.exe](../../release/GlobalUrlChecker.exe)。
2. 輸入公開網址，支援路徑、查詢字串及連接埠；省略協定使用 HTTPS。
3. 選取城市、GET／HEAD 及節點逾時秒數（5–30），按「開始測試」。預設 GET；部分網站不支援 HEAD。
4. 點選結果查看原始診斷及測量 ID，或匯出 CSV。

API Token 選填，僅保留在目前視窗記憶體，不寫入檔案。無 Token 使用受 Globalping 額度與速率限制；可減少城市、稍後再試或填入自己的 Token。最多同時處理 4 個城市。

### 測量說明

- 單次 HTTP 請求，並非包含圖片及 JavaScript 的完整頁面載入時間。耗時直接使用服務回傳值，首位元組欄位為其 firstByte 定義。
- HTTP 4xx／5xx 與連線失敗分開顯示；重新導向依服務狀態碼及診斷顯示。
- 精確指定城市及國家，無節點不會改用其他城市冒充。結果代表當下選中節點。
- API 422 可能是無節點或請求遭拒，詳見原始訊息。failed 狀態的逾時分類依診斷文字推估，應查看完整診斷確認。
- 服務等待逾時無法判定網站是否逾時。取消停止本機排程與等待，已提交的遠端測量可能繼續完成。
- 網址（含查詢字串）交給第三方 Globalping 與遠端節點，結果可能公開存取。勿輸入私人資訊或憑證。不支援本機、內網及需要登入的網址。

城市：台北、東京、首爾、香港、新加坡、孟買、曼谷、雅加達、雪梨、奧克蘭、杜拜、倫敦、巴黎、法蘭克福、阿姆斯特丹、紐約、洛杉磯、芝加哥、多倫多、聖保羅、聖地牙哥、約翰尼斯堡。

API 文件：[Globalping API](https://globalping.io/docs/api.globalping.io)、[官方 schema](https://github.com/jsdelivr/globalping/blob/master/public/v1/components/schemas.yaml)。

## 系統需求

Windows 10／11，.NET Framework 4.5 以上（一般 Windows 已有較新版本），以及可透過 HTTPS 存取 api.globalping.io 的網路。無須額外安裝程式或系統管理員權限，全球測量依賴外部節點服務。

## 重新建置

在專案根目錄執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-global-url-checker.ps1
```

成品：release/GlobalUrlChecker.exe。專屬圖示由 build-global-url-checker-icon.ps1 產生，來源保留在 assets/icons/。


驗證：執行 `tests/global-url-checker/run.ps1` 可跑本機檢查；加上 `-Live` 會向東京節點提交公開網址成功、不可連線連接埠及取消測量的測試，並消耗少量服務額度。預覽與測試暫存放在 `.build/global-url-checker/`。
