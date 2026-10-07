# 開發工具箱

## 功能

- Base64、URL、HTML、Hex 編碼與解碼
- MD5、SHA-1、SHA-256、SHA-384、SHA-512 雜湊
- JSON 美化格式化
- URL 元件解碼
- 結果一鍵複製

## 使用方式

直接執行 [`release/DevToolbox.exe`](../../release/DevToolbox.exe)，不需要安裝額外 runtime。輸入文字後選擇工具並按下操作按鈕。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-dev-toolbox.ps1
```

需求：Windows 10／11 及內建 .NET Framework 4.x C# 編譯器。
