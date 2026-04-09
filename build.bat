@echo off
cd /d "%~dp0"

echo Building Leroux Model Compiler...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin\publish

if %errorlevel% neq 0 (
    echo.
    echo BUILD FAILED. Make sure .NET 8 SDK is installed:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo Build successful!
echo Executable: bin\publish\LerouxCompiler.exe
echo.
echo Copying settings.json to output...
copy /y settings.json bin\publish\settings.json >nul

echo Done! Run bin\publish\LerouxCompiler.exe
pause
