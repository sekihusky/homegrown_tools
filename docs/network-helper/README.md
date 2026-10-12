# 網路修復小幫手

## 功能

- 一鍵清除 Windows DNS 快取：`ipconfig /flushdns`。
- 查看 DNS 快取、完整網路設定、路由表及 WinHTTP Proxy。
- 確認後更新 DHCP 租約：`ipconfig /renew`，不先執行 `/release`。
- 指定網域／IP 進行 DNS 解析或 Ping 測試。
- 複製結果或儲存 UTF-8 文字檔。

指令於背景執行，90 秒逾時。DNS 是否解析成功請查看回應內容；Ping 無回應可能是對方封鎖 ICMP。WinHTTP Proxy 不代表所有瀏覽器的代理設定。

## 使用方式

開啟 `release/NetworkHelper.exe`，允許系統管理員權限提示，按「一鍵清除 DNS 快取」，完成後重新開啟網頁。必要時查看設定或執行 DNS／Ping 測試。

清除快取不會刪除瀏覽紀錄或更改 DNS 伺服器，也不保證清除瀏覽器自己的 DNS 快取或讓外部 DNS 更新立即生效。

更新 DHCP 租約可能短暫影響連線，會先要求確認；固定 IP 網卡不適用。不會自動重設 Winsock／TCP/IP 或重開機。分享結果前請檢查內部 IP、MAC 與電腦名稱等資訊。

## 系統需求

Windows 10／11、.NET Framework 4.5 以上，啟動需要系統管理員權限。單一 EXE 免安裝，不需其他檔案。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-network-helper.ps1
```

腳本會產生專屬圖示並建置 `release/NetworkHelper.exe`。
