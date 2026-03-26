---
trigger: always_on
---

編集が完了したら、以下のコマンドを順に実行してアプリを再起動してください。


```powershell
Get-Process MouseGesture -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build D:\works\VisualStudio\WindowsFormsApp1\MouseGesture\MouseGesture.csproj; if ($?) { Start-Process "D:\works\VisualStudio\WindowsFormsApp1\MouseGesture\bin\Debug\net8.0-windows\win-x64\MouseGesture.exe" } 
