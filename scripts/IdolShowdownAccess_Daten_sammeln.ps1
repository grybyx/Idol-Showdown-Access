# Idol Showdown Access - data collector / Datensammler
# Packs the logs, settings and crash reports into one ZIP on the desktop (with date and time).
# Packt Protokolle, Einstellungen und Absturzberichte in eine ZIP auf dem Desktop (mit Datum und Uhrzeit).

$ErrorActionPreference = 'Continue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$work = Join-Path $env:TEMP "IdolShowdownAccess_Daten_$stamp"
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Grab($path, $sub) {
    if (-not (Test-Path $path)) { return }
    $dst = if ($sub) { Join-Path $work $sub } else { $work }
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    Copy-Item $path $dst -Recurse -Force -ErrorAction SilentlyContinue
}

$plug = Join-Path $here 'BepInEx\plugins\IdolShowdownAccess'
Grab (Join-Path $here 'BepInEx\LogOutput.log') 'BepInEx'
Grab (Join-Path $here 'BepInEx\config\de.access.idolshowdown.cfg') 'BepInEx'
Grab (Join-Path $here 'doorstop_config.ini') ''
foreach ($f in @('IdolShowdownAccess.log', 'IdolShowdownAccess_vorher.log', 'IdolShowdownAccessVoices.txt')) {
    Grab (Join-Path $plug $f) 'Mod'
    Grab (Join-Path $env:LOCALAPPDATA "IdolShowdownAccess\$f") 'Mod_LocalAppData'
}
# Unity player logs / Unity-Protokolle des Spiels
$low = Join-Path $env:USERPROFILE 'AppData\LocalLow'
Get-ChildItem $low -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    Get-ChildItem $_.FullName -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'Idol*' } | ForEach-Object {
        Grab (Join-Path $_.FullName 'Player.log') 'Unity'
        Grab (Join-Path $_.FullName 'Player-prev.log') 'Unity'
    }
}
# Unity crash folders (newest 3) / Absturzordner von Unity (die neuesten 3)
Get-ChildItem $env:TEMP -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    Get-ChildItem $_.FullName -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'Idol*' } | ForEach-Object {
        $c = Join-Path $_.FullName 'Crashes'
        if (Test-Path $c) { Get-ChildItem $c -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object { Grab $_.FullName 'Crashes' } }
    }
}
# Windows error reports (newest 3) / Windows-Fehlerberichte (die neuesten 3)
$wer = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\WER\ReportArchive'
Get-ChildItem $wer -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*IdolShowdown*' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object { Grab $_.FullName 'WER' }
# What is installed / Was installiert ist
Get-ChildItem (Join-Path $here 'unstripped_corlib') -ErrorAction SilentlyContinue | Select-Object Name, Length | Out-File (Join-Path $work 'unstripped_corlib.txt')
Get-ChildItem $plug -Recurse -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime | Out-File (Join-Path $work 'plugin_files.txt')

$desk = [Environment]::GetFolderPath('Desktop')
$zip = Join-Path $desk "IdolShowdownAccess_Daten_$stamp.zip"
Compress-Archive -Path (Join-Path $work '*') -DestinationPath $zip -Force
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Done. The file is on your desktop:"
Write-Host "Fertig. Die Datei liegt auf dem Desktop:"
Write-Host $zip
Read-Host "Press Enter / Enter drücken"
