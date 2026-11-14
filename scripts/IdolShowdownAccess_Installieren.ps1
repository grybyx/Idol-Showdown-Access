# Idol Showdown Access - installer / Installationshelfer
# Downloads the unstripped .NET and Unity libraries that BepInEx needs (the game ships stripped ones).
# Lädt die ungekürzten .NET- und Unity-Bibliotheken herunter, die BepInEx braucht (das Spiel bringt gekürzte mit).
# Run again after every update. / Nach jedem Update erneut ausführen.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

function Say($en, $de) { Write-Host $en; if ($de) { Write-Host $de } }

Say "Idol Showdown Access: installation." "Idol Showdown Access: Installation."
Write-Host ""

# 1. Game folder / Spielordner
$exe = Get-ChildItem -Path $here -Filter *.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch 'UnityCrashHandler' -and (Test-Path (Join-Path $here ($_.BaseName + '_Data\Managed\Assembly-CSharp.dll'))) } |
    Select-Object -First 1
if (-not $exe) {
    Say "ERROR: The game was not found in this folder. Unpack the mod directly into the Idol Showdown folder (where IdolShowdown.exe is) and run this again. In Steam: right-click the game, Manage, Browse local files." `
        "FEHLER: In diesem Ordner wurde das Spiel nicht gefunden. Entpacke den Mod direkt in den Spielordner von Idol Showdown (dort, wo IdolShowdown.exe liegt) und starte dies dort erneut. In Steam: Rechtsklick auf das Spiel, Verwalten, Lokale Dateien durchsuchen."
    Read-Host "Press Enter / Enter drücken"
    exit 1
}
Say ("Game found / Spiel gefunden: " + $exe.Name)

# 2. Unity version / Unity-Version
$uv = '2020.3.49'
foreach ($v in @($exe.VersionInfo.ProductVersion, $exe.VersionInfo.FileVersion)) {
    if ($v -and ($v -match '(\d{4})\.(\d+)\.(\d+)')) { $uv = "$($Matches[1]).$($Matches[2]).$($Matches[3])"; break }
}
Say ("Unity: " + $uv)

# 3. Unstripped libraries / Ungekürzte Bibliotheken
$target = Join-Path $here 'unstripped_corlib'
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

function Get-Zip($kind, $filter) {
    $url = "https://unity.bepinex.dev/$kind/$uv.zip"
    $zip = Join-Path $env:TEMP "idolshowdown_${kind}_$uv.zip"
    $tmp = Join-Path $env:TEMP "idolshowdown_${kind}_$uv"
    Say ("Downloading / Lade herunter: " + $url)
    try {
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
        if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
        Expand-Archive -Path $zip -DestinationPath $tmp -Force
        $n = 0
        Get-ChildItem -Path $tmp -Recurse -Filter *.dll | Where-Object { $_.Name -match $filter } | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $target $_.Name) -Force
            $n++
        }
        Remove-Item $zip -ErrorAction SilentlyContinue
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
        Say ("  " + $n + " files / Dateien")
        return $true
    } catch {
        Say "ERROR while downloading or unpacking:" "FEHLER beim Herunterladen oder Entpacken:"
        Write-Host $_.Exception.Message
        Say ("You can download it yourself and unpack the DLL files into the folder unstripped_corlib: " + $url) `
            ("Du kannst die Datei selbst herunterladen und die DLL-Dateien in den Ordner unstripped_corlib entpacken: " + $url)
        return $false
    }
}

$ok = $true
if (Test-Path (Join-Path $target 'mscorlib.dll')) {
    Say ".NET libraries already present." ".NET-Bibliotheken sind schon vorhanden."
} else {
    $ok = (Get-Zip 'corlibs' '\.dll$') -and $ok
}
if (Test-Path (Join-Path $target 'UnityEngine.CoreModule.dll')) {
    Say "Unity libraries already present." "Unity-Bibliotheken sind schon vorhanden."
} else {
    # Only the Unity engine modules, not game packages such as UnityEngine.UI
    $ok = (Get-Zip 'libraries' '^UnityEngine(\.[A-Za-z0-9]+Module)?\.dll$') -and $ok
}
if (-not $ok -or -not (Test-Path (Join-Path $target 'mscorlib.dll')) -or -not (Test-Path (Join-Path $target 'UnityEngine.CoreModule.dll'))) {
    Say "ERROR: The libraries are incomplete. The game would hang at the warning screen with the mod. To remove the mod, delete winhttp.dll in the game folder." `
        "FEHLER: Die Bibliotheken sind nicht vollständig. Das Spiel würde mit dem Mod beim Warnhinweis hängen. Zum Entfernen des Mods die Datei winhttp.dll im Spielordner löschen."
    Read-Host "Press Enter / Enter drücken"
    exit 1
}

# 4. Mod files / Mod-Dateien
$needed = @('winhttp.dll', 'doorstop_config.ini', 'BepInEx\core\BepInEx.Preloader.dll',
    'BepInEx\plugins\IdolShowdownAccess\IdolShowdownAccess.dll', 'BepInEx\plugins\IdolShowdownAccess\ISAccessSapi32.exe', 'BepInEx\plugins\IdolShowdownAccess\Tolk.dll')
$missing = $needed | Where-Object { -not (Test-Path (Join-Path $here $_)) }
if ($missing) {
    Say "ERROR: files are missing:" "FEHLER: Es fehlen Dateien:"
    $missing | ForEach-Object { Write-Host $_ }
    Read-Host "Press Enter / Enter drücken"
    exit 1
}
# Leftover from older versions (no longer used) / Rest älterer Versionen
Remove-Item (Join-Path $here 'BepInEx\plugins\IdolShowdownAccess\SAAPI64.dll') -ErrorAction SilentlyContinue

Write-Host ""
Say "Installation complete. Start Idol Showdown as usual through Steam." "Installation abgeschlossen. Starte Idol Showdown wie gewohnt über Steam."
Read-Host "Press Enter / Enter drücken"
