@echo off
cd /d "c:\Users\AHMAD\Downloads\Uni-Connect-master\Uni-Connect"
cls
echo.
echo ============================================
echo      UNI-CONNECT TEST CREDENTIALS
echo ============================================
echo.
echo Email:    20240001@philadelphia.edu.jo
echo Password: Test@1234
echo.
echo Admin:    ahmadalatrash726@gmail.com
echo Password: @Cometothebored1314@
echo.
echo Starting app at http://localhost:5282
echo.
start http://localhost:5282
timeout /t 2 /nobreak
dotnet run
pause
