param(
	[string]$Version = "",
	[string]$Source = "shintio",
	[string]$ApiKey = "az",
	[switch]$SkipDuplicate = $true,
	[switch]$NoPush
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
Set-Location $repoRoot

$projects = @(
	"src/Shintio.FileSystem.Abstractions/Shintio.FileSystem.Abstractions.csproj",
	"src/Shintio.FileSystem.Physical/Shintio.FileSystem.Physical.csproj"
)

if ([string]::IsNullOrWhiteSpace($Version)) {
	$Version = (dotnet msbuild "src/Shintio.FileSystem.Abstractions/Shintio.FileSystem.Abstractions.csproj" -nologo -getProperty:Version).Trim()
	if ([string]::IsNullOrWhiteSpace($Version)) {
		throw "Could not resolve package version from MSBuild."
	}
}

$outDir = Join-Path $repoRoot ("artifacts/nuget/" + $Version)
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

foreach ($project in $projects) {
	$packArgs = @(
		"pack", $project,
		"-c", "Release",
		"-o", $outDir,
		"-p:Version=$Version",
		"-p:IncludeSymbols=true",
		"-p:SymbolPackageFormat=snupkg",
		"--nologo"
	)
	dotnet @packArgs
}

Write-Host "Packed artifacts in $outDir"
Get-ChildItem $outDir -File | Sort-Object Name | Select-Object Name, Length | Format-Table -AutoSize

if ($NoPush) {
	Write-Host "NoPush is set. Skipping nuget push."
	exit 0
}

$pushArgs = @("--source", $Source, "--api-key", $ApiKey)
if ($SkipDuplicate) {
	$pushArgs += "--skip-duplicate"
}

dotnet nuget push (Join-Path $outDir "*.nupkg") @pushArgs
dotnet nuget push (Join-Path $outDir "*.snupkg") @pushArgs

Write-Host "Push completed to source '$Source' for version $Version."
