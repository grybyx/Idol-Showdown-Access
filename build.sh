#!/usr/bin/env sh
# Baut IdolShowdownAccess.dll und ISAccessSapi32.exe ohne .NET-SDK (Roslyn aus PowerShell 7).
# Benötigt: pwsh (PowerShell 7), die Managed-Bibliotheken des Spiels, BepInEx 5.4.23.2.
#   ISA_GAME_MANAGED=/pfad/IdolShowdown_Data/Managed \
#   ISA_BEPINEX_CORE=/pfad/BepInEx/core \
#   [ISA_ROSLYN_DIR=/pfad/zu/powershell] [ISA_OUT=./build] ./build.sh
# Unter Windows geht auch: dotnet build -c Release -p:GameDir="C:\...\Idol Showdown"
set -e
here="$(cd "$(dirname "$0")" && pwd)"
PWSH="${PWSH:-pwsh}"
exec "$PWSH" -NoProfile -File "$here/build.ps1"
