# Port 連線測試工具

免安裝、免啟用 Telnet 的 Windows TCP Port 測試工具。

## 功能

- 輸入 IP 位址或網域名稱與 Port 後直接測試 TCP 連線。
- 支援 IPv4、IPv6 與 DNS 網域解析。
- 可自訂 1～30 秒連線逾時。
- 顯示成功／失敗、連線耗時、實際連線 IP 與常見失敗原因。
- 保留目前執行期間的測試紀錄。
- 所有測試都在本機直接執行，不會將目標資料傳送到第三方服務。

## 使用方式

1. 執行 `release/PortChecker.exe`。
2. 輸入 IP 或網域，例如 `192.168.1.10`、`example.com`。
3. 輸入 Port，例如 HTTPS 使用 `443`、SSH 使用 `22`。
4. 按「測試」或 Enter，即可查看是否能建立 TCP 連線。

> 「連線成功」表示 TCP Port 可從這台電腦連到；不代表該服務的登入帳密或應用層功能一定正常。

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
