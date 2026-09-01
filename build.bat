@echo off
setlocal
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set GAC=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL
if not exist "dist" mkdir "dist"
if not exist "dist\build" mkdir "dist\build"

rem ---- 打包内嵌资源: web 前端 + WebView2 依赖 (单文件 exe 启动时解包到 %LOCALAPPDATA% ) ----
powershell -NoProfile -Command "Compress-Archive -Force -Path 'dist\web\*' -DestinationPath 'dist\build\webassets.zip'"
powershell -NoProfile -Command "Compress-Archive -Force -Path 'libs\Microsoft.Web.WebView2.Core.dll','libs\Microsoft.Web.WebView2.WinForms.dll','libs\WebView2Loader.dll' -DestinationPath 'dist\build\rundeps.zip'"
if not exist "dist\build\webassets.zip" ( echo PACK WEB FAILED & exit /b 1 )
if not exist "dist\build\rundeps.zip" ( echo PACK DEPS FAILED & exit /b 1 )

"%FW%\csc.exe" /nologo /optimize /target:winexe /out:dist\LLMTranslation.exe ^
  /win32manifest:src\app.manifest ^
  /win32icon:src\app.ico ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Net.Http.dll ^
  /reference:System.Runtime.Serialization.dll ^
  /reference:System.IO.Compression.dll ^
  /reference:System.IO.Compression.FileSystem.dll ^
  /resource:dist\build\webassets.zip,webassets ^
  /resource:dist\build\rundeps.zip,rundeps ^
  /reference:libs\Microsoft.Web.WebView2.Core.dll ^
  /reference:libs\Microsoft.Web.WebView2.WinForms.dll ^
  /reference:"%GAC%\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll" ^
  /reference:"%GAC%\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll" ^
  /reference:"%GAC%\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" ^
  src\Program.cs src\NativeMethods.cs src\Config.cs src\Models.cs src\Translator.cs src\Dpi.cs ^
  src\FloatingButtonForm.cs src\TranslateForm.cs src\SettingsForms.cs src\TrayHotkey.cs src\AppContext.cs src\WinLog.cs

if %errorlevel% neq 0 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

"%FW%\csc.exe" /nologo /optimize /target:winexe /out:dist\TestTarget.exe ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  src\TestTarget.cs

if %errorlevel% equ 0 (
  copy /y libs\Microsoft.Web.WebView2.Core.dll dist\ >nul
  copy /y libs\Microsoft.Web.WebView2.WinForms.dll dist\ >nul
  copy /y libs\WebView2Loader.dll dist\ >nul
  echo.
  echo BUILD OK -^> dist\LLMTranslation.exe single-file ~1.2MB, dist\TestTarget.exe
) else (
  echo.
  echo BUILD FAILED
  exit /b 1
)
endlocal
