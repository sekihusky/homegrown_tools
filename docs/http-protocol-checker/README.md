# HTTP 協定檢測

## 功能

輸入網址實測 HTTP/1.1、HTTP/2、HTTP/3，顯示實際協定、HTTP 狀態碼、完整請求耗時及錯誤原因，支援複製結果。

## 使用方式

開啟 `release/HttpProtocolChecker/HttpProtocolChecker.exe`，輸入網址後按 Enter 或「開始檢測」。省略協定時使用 HTTPS。請複製整個資料夾使用。

- 支援（實測成功）：收到該協定回應，403、404、405、500 等狀態也可確認協定。
- 本次未協商到：實際回應為其他協定。
- 未確認：DNS、憑證、逾時或網路錯誤，不能直接判定不支援。
- 不適用：HTTP/3 需要 HTTPS。

直接發送 HEAD 請求，不使用環境代理、不下載頁面內容、不跟隨轉址。結果代表輸入網址的端點與目前網路；轉址目的網址需另外測試。HTTP/2 明文連線使用 h2c Upgrade。HTTP/3 使用 `--http3-only` 禁止降級，僅測指定端點，不探索其他 Alt-Svc 端點；防火牆封鎖 UDP 可能造成逾時。保留 TLS 憑證驗證。

三個請求並行，每個上限 20 秒，程式最長約 25 秒收尾。耗時是完整請求時間，不是 Ping 延遲。

## 系統需求

Windows 10／11 x64、.NET Framework 4.5 以上及網路。隨附 curl，無需另外安裝。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-http-protocol-checker.ps1
```

下載 curl 8.23.0_4 x64 並驗證固定 SHA256，產生專屬圖示與 EXE。下載快取在 `.build`，解壓內容在 `packages/http-protocol-curl`。授權與相依元件資訊隨成品放在 `COPYING.txt`、`dep`、`BUILD-MANIFEST.txt`。

參考：[curl Windows](https://curl.se/windows/)、[curl 參數文件](https://curl.se/docs/manpage.html)。
