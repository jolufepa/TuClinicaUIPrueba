@echo off
echo --- LIMPIEZA TOTAL DE TUCLINICA ---
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
echo (Si ves un error de 'Acceso Denegado' arriba, ignoralo)
echo.

echo --- ¡LIMPIEZA COMPLETADA! ---
echo.
echo AHORA:
echo 1. Abre TuClinica.sln
echo 2. Clic derecho en la Solucion > 'Restaurar paquetes NuGet'
echo 3. Clic derecho en la Solucion > 'Recompilar solucion'
echo.
pause