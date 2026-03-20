@echo off
echo Building asset-monitoring...
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\IT Cell\source\repos\asset-monitoring\asset-monitoring.csproj" /p:Configuration=Debug /t:Build /v:minimal
echo.
echo Build complete. Exit code: %ERRORLEVEL%
pause
