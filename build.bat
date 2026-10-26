@echo off
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:"%~dp0TufFanControl.exe" /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll "%~dp0TufFanControl.cs"
if %ERRORLEVEL% EQU 0 (
    echo Build succeeded: TufFanControl.exe
) else (
    echo Build failed.
)
