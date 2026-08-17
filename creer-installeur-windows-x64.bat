@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "LABEL=Beta-1.1"
set "ISS=Installer\Hephaistos.iss"
set "PORTABLE=dist\Hephaistos-%LABEL%-Windows-x64"
set "INSTALLERDIR=dist\installer"
set "INSTALLER=%INSTALLERDIR%\Hephaistos-%LABEL%-Windows-x64-Installer.exe"

cls
echo ==============================================
echo   HEPHAISTOS BETA 1.1 - CREATION INSTALLATEUR
echo ==============================================
echo.
echo Dossier du projet : %CD%
echo.

if not exist ".\hephaistos.csproj" (
    echo ERREUR : hephaistos.csproj est introuvable.
    echo Placez ce fichier a la racine du projet Hephaistos.
    echo.
    pause
    exit /b 1
)

if not exist ".\%ISS%" (
    echo ERREUR : %ISS% est introuvable.
    echo Reextrayez le patch installateur a la racine du projet.
    echo.
    pause
    exit /b 1
)

echo [1/3] Creation de la version Windows autonome...
call ".\publier-windows-x64.bat" --nopause
if errorlevel 1 (
    echo.
    echo ERREUR : la publication d'Hephaistos a echoue.
    echo.
    pause
    exit /b 1
)

if not exist ".\%PORTABLE%\Hephaistos.exe" (
    echo.
    echo ERREUR : la version portable est introuvable :
    echo   %PORTABLE%\Hephaistos.exe
    echo.
    pause
    exit /b 1
)

echo.
echo [2/3] Recherche du compilateur Inno Setup 7...
set "ISCC="

for %%I in (ISCC.exe) do if not defined ISCC set "ISCC=%%~$PATH:I"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
if not defined ISCC if exist "%LocalAppData%\Programs\Inno Setup 7\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 7\ISCC.exe"

if not defined ISCC (
    echo Inno Setup 7 n'est pas encore installe sur ce PC de developpement.
    echo Il sert uniquement a FABRIQUER l'installateur ; les utilisateurs finaux
    echo n'auront pas besoin de l'installer.
    echo.

    where winget >nul 2>nul
    if not errorlevel 1 (
        echo Installation automatique d'Inno Setup 7 avec winget...
        winget install --id JRSoftware.InnoSetup.7 -e -s winget --accept-package-agreements --accept-source-agreements
        echo.

        for %%I in (ISCC.exe) do if not defined ISCC set "ISCC=%%~$PATH:I"
        if not defined ISCC if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
        if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
        if not defined ISCC if exist "%LocalAppData%\Programs\Inno Setup 7\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 7\ISCC.exe"
    )
)

if not defined ISCC (
    echo.
    echo ERREUR : Inno Setup 7 reste introuvable.
    echo La page officielle va s'ouvrir. Installez Inno Setup 7 x64 puis
    echo relancez simplement ce fichier .bat.
    start "" "https://jrsoftware.org/isdl.php"
    echo.
    pause
    exit /b 1
)

echo Compilateur : %ISCC%

echo.
echo [3/3] Compilation de l'installateur Hephaistos...
if exist ".\%INSTALLERDIR%" rmdir /s /q ".\%INSTALLERDIR%"
mkdir ".\%INSTALLERDIR%" >nul 2>nul

"%ISCC%" ".\%ISS%"
if errorlevel 1 (
    echo.
    echo ERREUR : Inno Setup n'a pas pu creer l'installateur.
    echo.
    pause
    exit /b 1
)

if not exist ".\%INSTALLER%" (
    echo.
    echo ERREUR : l'installateur attendu est introuvable :
    echo   %INSTALLER%
    echo.
    pause
    exit /b 1
)

echo.
echo ==============================================
echo   INSTALLATEUR CREE AVEC SUCCES
echo ==============================================
echo.
echo Fichier a transmettre a l'utilisateur :
echo   %CD%\%INSTALLER%
echo.
echo L'utilisateur n'a qu'a double-cliquer sur ce fichier.
echo Hephaistos sera installe dans son profil Windows, avec raccourci
echo menu Demarrer et raccourci Bureau si l'option est conservee.
echo.
echo IMPORTANT : cet installateur Beta 1.1 n'est pas signe avec un certificat
echo Authenticode. Windows peut donc afficher "Editeur inconnu" / SmartScreen.
echo Cela n'empeche pas le test fonctionnel.
echo.
start "" "%CD%\%INSTALLERDIR%"
pause
endlocal
