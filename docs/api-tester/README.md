# API 測試工具

免安裝 Windows HTTP API 測試工具，適合手動呼叫與檢查外部 API。

## 功能

- GET、POST、PUT、PATCH、DELETE、HEAD、OPTIONS。
- Query 參數表格（自動 URL 編碼）、自訂 Headers。
- JSON、純文字、XML、URL encoded Body；URL encoded 請自行填入 `key=value&key2=value2`。
- Bearer Token、Basic Auth；也可以透過 Headers 傳送 API Key。
- 顯示 HTTP 狀態、耗時、解壓後大小、最後網址、回應文字與 Headers。
- JSON 格式化、複製及另存回應文字。
- 1–300 秒逾時、取消請求、選擇是否跟隨重新導向。
- 請求收藏與本次啟動最近 50 筆成功收到 HTTP 回應的紀錄（包含 4xx / 5xx）。

## 使用方式

1. 執行 `release/ApiTester.exe`。
2. 選擇 HTTP 方法並輸入完整的 `https://` 或 `http://` 網址。
3. 在 Query、Headers、Body、驗證頁籤填入請求內容，按「送出請求」。表格最後一列可繼續新增。
4. 在下方查看回應；HTTP 4xx / 5xx 會正常顯示伺服器回傳內容。
5. 輸入收藏名稱並按「儲存」，同名收藏會更新。雙擊左側收藏或紀錄即可載入，再按送出。

收藏包含驗證資料，使用 Windows DPAPI 以目前帳號加密，儲存在 `%LOCALAPPDATA%\HomegrownTools\ApiTester\requests.dat`；無法直接搬至其他帳號解密。歷史紀錄只保留在記憶體。收藏不包含逾時與重新導向選項。

每次請求獨立，不自動保存 Cookie；如需 Cookie，請自行填入 Header。驗證頁籤的設定優先於 Authorization Header。系統代理伺服器設定由 .NET 處理，HTTPS 使用系統憑證驗證。

此版本以文字 API 為主，不支援 multipart 檔案上傳、二進位預覽、OAuth 登入流程、Postman Collection 匯入或自動測試腳本。回應緩衝上限為 10 MiB，超過將顯示請求失敗；另存功能輸出解碼後的文字，不是原始二進位檔案。

## 系統需求

- Windows 10 / 11，.NET Framework 4.7.2 或更新版本。
- 直接執行單檔 EXE，不需要安裝 Postman 或其他套件。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-api-tester.ps1
```

腳本產生專屬圖示並以 Windows 內建 .NET Framework 編譯器建置 `release/ApiTester.exe`。
