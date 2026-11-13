Option Explicit
Dim shell, fso, folder, app, result
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
folder = fso.GetParentFolderName(WScript.ScriptFullName)
app = fso.BuildPath(folder, "publish\YandexMusicController.exe")
If fso.FileExists(app) Then
  shell.Run Chr(34) & app & Chr(34) & " --background", 0, False
Else
  result = shell.Run("cmd /c where dotnet >nul 2>nul", 0, True)
  If result = 0 Then
    shell.Run Chr(34) & fso.BuildPath(folder, "start.bat") & Chr(34), 0, False
  Else
    MsgBox "Готовая версия EXE ещё не собрана. Для первой сборки установите .NET 8 SDK и запустите start.bat.", vbInformation, "Yandex Music Controller"
    shell.Run Chr(34) & fso.BuildPath(folder, "start.bat") & Chr(34), 1, False
  End If
End If
