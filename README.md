# Homegrown Tools

這裡收錄自行開發的 Windows 小工具。每個工具都可獨立使用，詳細功能、使用方式與建置指令請見各自的 README。

## 工具列表

| 工具 | 用途 | 執行檔 | 文件 |
| --- | --- | --- | --- |
| 網域 IP / DNS 查詢工具 | 查詢網域註冊日期、IP、ISP／ASN、地區及各類 DNS 紀錄 | [`DomainDnsTool.exe`](release/DomainDnsTool.exe) | [使用說明](docs/domain-dns-tool/README.md) |
| Hosts 檔案編輯器 | 搜尋、編輯、停用及備份 Windows hosts 規則 | [`HostsFileEditor.exe`](release/HostsFileEditor.exe) | [使用說明](docs/hosts-file-editor/README.md) |

## 使用需求

- Windows 10 或 Windows 11
- Windows 內建的 .NET Framework 4.x
- 工具皆為單一 EXE，不需要安裝

個別工具可能需要網路連線或系統管理員權限，請參閱各自的使用說明。

## 新增工具的文件規則

為了讓工具數量增加後仍容易瀏覽，新增工具時請遵循以下規則：

1. 使用小寫英文與連字號建立 `docs/<工具代號>/README.md`，例如 `docs/port-checker/README.md`。
2. README 至少包含「功能」、「使用方式」、「系統需求」及「重新建置」。
3. 執行檔統一放在 `release/`，檔名應能直接辨識工具。
4. 在本頁的「工具列表」新增一列，連結執行檔與個別文件。
5. 原始碼及建置腳本使用一致的工具名稱，避免新增後難以配對。
