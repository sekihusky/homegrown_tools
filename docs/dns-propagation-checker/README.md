# DNS 更新檢查工具

[返回工具總覽](../../README.md)

## 功能

輸入完整網域，並行查詢各 DNS 的 A（IPv4）與 AAAA（IPv6）紀錄，顯示各服務回應、錯誤及是否一致。

內建 Google、Cloudflare、Quad9、Cisco OpenDNS（主要／備用）、AdGuard、CleanBrowsing、中華電信 HiNet（主要／備用）、NTT Communications、KDDI、SoftBank、AliDNS（主要／備用）、114DNS（主要／備用）、韓國 SK Broadband 與 KT Corp、DNS4EU，以及 ThaiNS（IPv4／IPv6）。

114DNS 是獨立服務，並非騰訊 DNS。部分電信商 DNS 可能僅對自家網路提供遞迴查詢；遇到逾時或拒絕時，工具會顯示查詢失敗。

結果只代表這些公共遞迴 DNS 在查詢當下的回應，不能證明全球每個 DNS 快取都已更新。CDN、地理位置及負載平衡也可能造成正常的 IP 差異。

## 使用方式

執行 [`release/DnsPropagationChecker.exe`](../../release/DnsPropagationChecker.exe)，輸入 `example.com` 後按「查詢」。可將單一 EXE 複製到其他 Windows 電腦使用。網路需允許 UDP 53 對外連線。

## 系統需求

- Windows 10 或 Windows 11
- 內建 .NET Framework 4.x
- 網路連線

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-dns-propagation-checker.ps1
```

成品位於 `release/DnsPropagationChecker.exe`。
