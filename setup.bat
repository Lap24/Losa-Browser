@echo off
echo You need .NET 6.0 Desktop Runtime.
echo Opening download page...
start https://dotnet.microsoft.com/en-us/download/dotnet/6.0

echo.
echo Press ENTER once you have installed .NET 6.0.
pause

echo Building Losa Browser...
dotnet build -c Release

echo Done!
pause
exit
