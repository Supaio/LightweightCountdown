@echo off
setlocal
cd /d "%~dp0"

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo [ERROR] Windows .NET Framework compiler was not found.
  echo Please enable .NET Framework 4.x in Windows Features.
  pause
  exit /b 1
)

if not exist "bin" mkdir "bin"

set "OUTPUT=bin\Counter.exe"
if not "%~1"=="" set "OUTPUT=%~1"

"%CSC%" /nologo /utf8output /target:winexe /optimize+ /debug- /platform:anycpu ^
  /win32manifest:app.manifest ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /out:"%OUTPUT%" ^
  Program.cs CountdownItem.cs CountdownSchedule.cs AppSettings.cs StartupManager.cs UiControls.cs CountdownEditorDialog.cs WidgetForm.cs

if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

echo.
echo Build succeeded: %OUTPUT%
exit /b 0
