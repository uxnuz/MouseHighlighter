@echo off
chcp 65001 >nul
rem 코드를 고친 뒤 이 파일을 더블클릭하면 MouseHighlighter.exe를 새로 만듭니다.
rem 폴더를 어디로 옮겨도 동작하도록 이 .bat 파일이 있는 위치를 기준으로 합니다.
cd /d "%~dp0"

rem 실행 중이면 exe 파일이 잠겨서 덮어쓸 수 없으므로 먼저 종료
taskkill /im MouseHighlighter.exe /f >nul 2>&1

echo MouseHighlighter.exe 만드는 중...
dotnet publish "MouseHighlighter.csproj" -p:PublishProfile=Share -nologo -v:q
if errorlevel 1 (
    echo.
    echo 실패했습니다. 위 오류 내용을 확인하세요.
    echo .NET SDK가 설치되어 있어야 합니다: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

rem 빌드 중간 파일 정리 (다음에 다시 만들어지므로 지워도 괜찮음)
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

echo.
echo 완료: %~dp0MouseHighlighter.exe
pause
