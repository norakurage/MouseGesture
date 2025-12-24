---
trigger: always_on
---

編集が完了したら以下のコマンドを実行してアプリの再起動をしてください｡
1. Get-Process WindowsFormsApp1 -ErrorAction SilentlyContinue | Stop-Process -Force
2. dotnet build
3. WindowsFormsApp1\bin\Debug\MouseGesture.exe - ショートカット.lnk"
