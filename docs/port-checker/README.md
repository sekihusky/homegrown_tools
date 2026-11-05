# Port 連線測試工具

免安裝、免啟用 Telnet 的 Windows TCP Port 測試工具。

## 功能

- 輸入 IP 位址或網域名稱與 Port 後直接測試 TCP 連線。
- 支援 IPv4、IPv6 與 DNS 網域解析。
- 可自訂 1～30 秒連線逾時。
- 顯示成功／失敗、連線耗時、實際連線 IP 與常見失敗原因。
- 可附加執行 SSL/TLS 握手檢查，顯示協定版本、憑證名稱、到期日及驗證狀態。
- 能區分「TCP Port 可通但不是 SSL/TLS」與「SSL/TLS 可正常握手」。
- 可檢查 HTTP 根目錄的回應是否直接跳轉到相同主機、相同 Port 的 HTTPS，並顯示狀態碼與目標網址。
- 保留目前執行期間的測試紀錄。
- 所有測試都在本機直接執行，不會將目標資料傳送到第三方服務。

## 使用方式

1. 執行 `release/PortChecker.exe`。
2. 輸入 IP 或網域，例如 `192.168.1.10`、`example.com`。
3. 輸入 Port，例如 HTTPS 使用 `443`、SSH 使用 `22`。
4. 若要確認 SSL，勾選「附加檢查 SSL/TLS（握手與憑證）」。
5. 如需檢查 HTTP 轉 HTTPS，勾選「檢查 HTTP 是否跳轉 HTTPS（同一 Port）」。
6. 按「測試」或 Enter，即可查看結果。

跳轉檢查對 `http://主機:Port/` 發出 GET 請求，檢查第一個回應的 Location，不自動追蹤跳轉，也不代表 HTTPS 目標一定可用；可搭配 SSL/TLS 檢查確認握手。

> 「連線成功」表示 TCP Port 可從這台電腦連到；不代表該服務的登入帳密或應用層功能一定正常。

> 自簽、過期或名稱不符的憑證仍可能完成 TLS 握手。工具會將「SSL 可通」與「憑證驗證有問題」分開顯示。

## 系統需求

- Windows 10 或 Windows 11
- Windows 內建的 .NET Framework 4.x
- 不需要 Telnet Client，也不需要系統管理員權限

## 重新建置

在 PowerShell 中執行：

```powershell
.\build-port-checker.ps1
```

建置完成後會產生 `release/PortChecker.exe`。
