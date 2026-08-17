@echo off
setlocal EnableExtensions

set "NOPAUSE=0"
if /I "%~1"=="--nopause" set "NOPAUSE=1"

rem Toujours travailler depuis le dossier ou se trouve ce script.
cd /d "%~dp0"

set "APPNAME=Hephaistos"
set "RID=win-x64"
set "LABEL=Beta-1.1"
set "DISTROOT=%CD%\dist"
set "OUTDIR=%DISTROOT%\Hephaistos-%LABEL%-Windows-x64"
set "ZIPFILE=%DISTROOT%\Hephaistos-%LABEL%-Windows-x64.zip"
set "TESSDIR=%CD%\tessdata"
set "TESSFILE=%TESSDIR%\fra.traineddata"

cls
echo ==============================================
echo   HEPHAISTOS BETA 1.1 - WINDOWS x64
echo ==============================================
echo.
echo Dossier du projet : %CD%
echo.

if not exist ".\hephaistos.csproj" (
    echo ERREUR : hephaistos.csproj est introuvable.
    echo Placez ce script a la racine du projet Hephaistos.
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERREUR : le SDK .NET n'est pas installe sur ce PC de developpement.
    echo Les utilisateurs finaux n'en ont pas besoin.
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 1
)

if not exist "%TESSFILE%" (
    echo Le modele OCR francais est absent.
    echo Telechargement automatique de fra.traineddata...
    echo.

    if not exist "%TESSDIR%" mkdir "%TESSDIR%"

    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "$ErrorActionPreference='Stop'; Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/fra.traineddata' -OutFile '%TESSFILE%'"

    if errorlevel 1 (
        echo.
        echo ERREUR : impossible de telecharger tessdata\fra.traineddata.
        echo Verifiez votre connexion Internet puis relancez ce fichier.
        echo.
        if "%NOPAUSE%"=="0" pause
        exit /b 1
    )
)

if not exist "%TESSFILE%" (
    echo.
    echo ERREUR : tessdata\fra.traineddata est introuvable.
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 1
)

echo Modele OCR : OK

echo.
echo Nettoyage de l'ancienne distribution Beta 1.1...
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
if exist "%ZIPFILE%" del /q "%ZIPFILE%"
if not exist "%DISTROOT%" mkdir "%DISTROOT%"

echo.
echo Publication de l'application...
dotnet publish ".\hephaistos.csproj" ^
  -c Release ^
  -r %RID% ^
  --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:PublishTrimmed=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUTDIR%"

if errorlevel 1 (
    echo.
    echo ERREUR : dotnet publish a echoue.
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 1
)

rem Securite : s'assurer que le modele OCR est bien present dans la distribution.
if not exist "%OUTDIR%\tessdata" mkdir "%OUTDIR%\tessdata"
copy /y "%TESSFILE%" "%OUTDIR%\tessdata\fra.traineddata" >nul

rem La cible est win-x64 : supprimer les symboles de debug et binaires x86 inutiles.
if exist "%OUTDIR%\x86" rmdir /s /q "%OUTDIR%\x86"
for /r "%OUTDIR%" %%F in (*.pdb) do del /q "%%F" >nul 2>nul

if exist ".\LIRE-MOI-DISTRIBUTION.txt" (
    copy /y ".\LIRE-MOI-DISTRIBUTION.txt" "%OUTDIR%\LIRE-MOI.txt" >nul
)

if not exist "%OUTDIR%\%APPNAME%.exe" (
    echo.
    echo ERREUR : %APPNAME%.exe est introuvable dans la distribution.
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 1
)

if not exist "%OUTDIR%\tessdata\fra.traineddata" (
    echo.
    echo ERREUR : le modele OCR n'a pas ete inclus dans la distribution.
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 1
)

echo.
echo Creation du ZIP Beta 1.1...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop'; Compress-Archive -Path '%OUTDIR%\*' -DestinationPath '%ZIPFILE%' -CompressionLevel Optimal -Force"
if errorlevel 1 (
    echo.
    echo ERREUR : impossible de creer le ZIP.
    echo Le dossier portable reste utilisable :
    echo   %OUTDIR%
    echo.
    if "%NOPAUSE%"=="0" pause
    exit /b 0
)

echo.
echo ==============================================
echo   HEPHAISTOS BETA 1.1 CREE AVEC SUCCES
echo ==============================================
echo.
echo Dossier :
echo   %OUTDIR%
echo.
echo ZIP :
echo   %ZIPFILE%
echo.
echo Testez d'abord :
echo   %OUTDIR%\%APPNAME%.exe
echo.
echo Puis testez le ZIP sur un PC Windows x64 propre,
echo idealement sans .NET, sans Ollama et sans modeles.
echo.
if "%NOPAUSE%"=="0" pause
endlocal
