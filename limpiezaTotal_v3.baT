@echo off
echo --- LIMPIEZA TOTAL DE TUCLINICA (V3) ---
echo.
echo Cerrando Visual Studio y procesos MSBuild...
taskkill /f /im devenv.exe /t > nul 2>&1
taskkill /f /im msbuild.exe /t > nul 2>&1
echo.

echo Eliminando carpetas .vs, bin, y obj de la solucion...
rmdir /s /q ".vs" > nul 2>&1
for /d /r . %%d in (bin, obj) do @if exist "%%d" (rmdir /s /q "%%d")
echo.

echo Limpiando cache global de paquetes NuGet...
dotnet nuget locals all --clear
echo.

echo Eliminando especificamente el paquete QuestPDF cacheado...
rmdir /s /q "%USERPROFILE%\.nuget\packages\questpdf"
echo.

echo Eliminando caches de componentes de Visual Studio...
rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\ComponentModelCache" > nul 2>&1
echo.

echo Eliminando cache de MSBuild...
rmdir /s /q "%LOCALAPPDATA%\Microsoft\MSBuild\fast_binary_log" > nul 2>&1
echo.

echo Eliminando archivos de restauracion de la solucion...
del /s /q "project.assets.json" > nul 2>&1
del /s /q "project.lock.json" > nul 2>&1
del /s /q "*.nupkg.metadata" > nul 2>&1
rmdir /s /q "packages" > nul 2>&1
echo.


echo --- ¡LIMPIEZA COMPLETADA! ---
echo.
echo SIGUIENTES PASOS (MUY IMPORTANTE):
echo 1. Abre TuClinica.sln en Visual Studio.
echo 2. Ve a la PESTANA 'INSTALADOS' del Administrador de Paquetes NuGet.
echo 3. Verifica que QuestPDF esta en la version 2024.3.6.
echo 4. Recompila la solucion.
echo.
pause