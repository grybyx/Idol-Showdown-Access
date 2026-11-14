# Build für IdolShowdownAccess ohne .NET-SDK: Roslyn aus PowerShell 7.
# Pfade kommen aus Umgebungsvariablen (siehe build.sh):
#   ISA_GAME_MANAGED  = <Spiel>\IdolShowdown_Data\Managed  (gekürzte Spielbibliotheken, absichtlich)
#   ISA_BEPINEX_CORE  = BepInEx\core (BepInEx 5.4.23.2)
#   ISA_ROSLYN_DIR    = Ordner mit Microsoft.CodeAnalysis*.dll (z. B. PowerShell-7-Installationsordner)
#   ISA_OUT           = Ausgabeordner (Standard: ./build)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$G = $env:ISA_GAME_MANAGED; $B = $env:ISA_BEPINEX_CORE; $R = $env:ISA_ROSLYN_DIR
if (-not $R) { $R = Split-Path -Parent (Get-Process -Id $PID).Path }
$Out = if ($env:ISA_OUT) { $env:ISA_OUT } else { Join-Path $root 'build' }
if (-not $G -or -not (Test-Path (Join-Path $G 'Assembly-CSharp.dll'))) { throw "ISA_GAME_MANAGED zeigt nicht auf den Managed-Ordner des Spiels." }
if (-not $B -or -not (Test-Path (Join-Path $B 'BepInEx.dll'))) { throw "ISA_BEPINEX_CORE zeigt nicht auf BepInEx\core." }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
Add-Type -Path (Join-Path $R 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $R 'Microsoft.CodeAnalysis.CSharp.dll')

function Compile($name, $sources, $refPaths, $kind, $platform) {
    $refs = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]'
    foreach ($p in $refPaths) { $refs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($p)) }
    $po = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
    $trees = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]'
    foreach ($f in $sources) {
        $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($f), $po, $f, [Text.Encoding]::UTF8))
    }
    $opts = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new($kind).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithPlatform($platform).WithAllowUnsafe($true)
    $comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($name, $trees, $refs, $opts)
    $ext = if ($kind -eq [Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication) { '.exe' } else { '.dll' }
    $path = Join-Path $Out ($name + $ext)
    $fs = [IO.File]::Create($path); $res = $comp.Emit($fs); $fs.Close()
    $errs = 0
    foreach ($d in $res.Diagnostics) {
        if ($d.Severity -eq 'Error') { $errs++; Write-Output ("ERROR " + $d) }
        elseif ($d.Severity -eq 'Warning' -and $d.Id -notin @('CS1701','CS1702','CS0649','CS0169','CS0414','CS0162')) { Write-Output ("warn  " + $d) }
    }
    if ($res.Success) { Write-Output "BUILD OK -> $path" } else { Remove-Item $path -ErrorAction SilentlyContinue; Write-Output "BUILD FAILED $name ($errs)"; exit 1 }
}

# 1) Plugin
$names = @('mscorlib','System','System.Core','UnityEngine','UnityEngine.CoreModule','UnityEngine.UI','UnityEngine.UIModule',
  'UnityEngine.InputLegacyModule','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.AnimationModule',
  'Unity.TextMeshPro','Assembly-CSharp','Unity.Mathematics.FixedPoint','Unity.Mathematics','AK.Wwise.Unity.API.WwiseTypes','AK.Wwise.Unity.API')
$refs = @($names | ForEach-Object { Join-Path $G ($_ + '.dll') }) + @((Join-Path $B 'BepInEx.dll'), (Join-Path $B '0Harmony.dll'))
Compile 'IdolShowdownAccess' (Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName) $refs ([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary) ([Microsoft.CodeAnalysis.Platform]::AnyCpu)

# 2) 32-Bit-Sprachhelfer (gegen dieselbe mscorlib 4.x)
Compile 'ISAccessSapi32' @((Join-Path $root 'sapi32/Program.cs')) @((Join-Path $G 'mscorlib.dll')) ([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication) ([Microsoft.CodeAnalysis.Platform]::X86)
