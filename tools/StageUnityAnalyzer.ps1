param(
    [string]$Configuration = "Release"
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$generatorProject = Join-Path $repoRoot "InjekkoGen\InjekkoGen.csproj"
$packageAnalyzerDir = Join-Path $repoRoot "UnityPackage\com.extrys.injekko\Analyzers"
$includedDlls = @(
    "InjekkoGen.dll",
    "Microsoft.CodeAnalysis.dll",
    "Microsoft.CodeAnalysis.CSharp.dll"
)

dotnet build $generatorProject -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path $packageAnalyzerDir | Out-Null
$buildOutputDir = Join-Path $repoRoot "InjekkoGen\bin\$Configuration\netstandard2.0"

Get-ChildItem -Path $packageAnalyzerDir -Filter *.dll -File | Remove-Item -Force
Get-ChildItem -Path $packageAnalyzerDir -Filter *.dll.meta -File | Where-Object {
    $includedDlls -notcontains $_.BaseName
} | Remove-Item -Force

Get-ChildItem -Path $buildOutputDir -Filter *.dll -File | Where-Object {
    $includedDlls -contains $_.Name
} | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $packageAnalyzerDir $_.Name) -Force
}

Write-Host "Analyzer staged to $packageAnalyzerDir with Roslyn compiler assemblies only"
