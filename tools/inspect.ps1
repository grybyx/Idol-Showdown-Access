# Aufruf: pwsh inspect.ps1 find <Teil> | members <Typ>... | enum <Typ>...
$R = $env:ISA_ROSLYN_DIR; $G = $env:ISA_GAME_MANAGED
$ca = Join-Path $R 'Microsoft.CodeAnalysis.dll'; $cs = Join-Path $R 'Microsoft.CodeAnalysis.CSharp.dll'
Add-Type -Path $ca; Add-Type -Path $cs
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Inspect.cs'))) -ReferencedAssemblies @($ca, $cs, 'System.Collections', 'System.Linq', 'System.Runtime', 'System.Collections.Immutable', 'netstandard') -IgnoreWarnings -WarningAction SilentlyContinue
$mode = $args[0]
foreach ($a in $args[1..($args.Length-1)]) {
  switch ($mode) { 'find' { [Inspect]::Find($G, $a) } 'members' { [Inspect]::Members($G, $a) } 'enum' { [Inspect]::Enum($G, $a) } }
}
