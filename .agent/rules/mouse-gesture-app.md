---
trigger: always_on
---

編集が完了したら、以下のコマンドを順に実行してアプリを再起動してください。


```powershell
# 実行中のアプリを強制終了
Get-Process MouseGesture -ErrorAction SilentlyContinue | Stop-Process -Force

# プロジェクトを明示的にビルド
dotnet build D:\works\VisualStudio\WindowsFormsApp1\MouseGesture\MouseGesture.csproj `
&& D:\works\VisualStudio\WindowsFormsApp1\MouseGesture\bin\Debug\MouseGesture.exe