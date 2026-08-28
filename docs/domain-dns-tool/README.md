# 網域 IP / DNS 查詢工具

[返回工具總覽](../../README.md)

## 功能

輸入網域後，可顯示：

- 網域對應的 IPv4 / IPv6 位址
- IP 所屬 ISP／組織、ASN 與推估地區
- 管理該網域的 DNS 主機（NS 紀錄）
- 其他公開 DNS 紀錄：CNAME、MX、TXT、SOA、CAA、SRV、DS、DNSKEY

## 使用方式

執行 [`release/DomainDnsTool.exe`](../../release/DomainDnsTool.exe)。可將這個 EXE 複製到其他 Windows 電腦直接使用。

程式需要網路連線。IP 地區屬於公開 IP 資料庫的推估結果，代表網路出口或資料中心的大致位置，不是伺服器的精確地址。

## 系統需求

- Windows 10 或 Windows 11
- Windows 內建的 .NET Framework 4.x
- 網路連線

## 重新建置

在專案根目錄開啟 PowerShell，執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

成品會產生在 `release/DomainDnsTool.exe`。
