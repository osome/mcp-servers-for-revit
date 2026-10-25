# Builds the Revit 2026 add-in and installs it into the current user's Revit 2026 add-ins folder.
# Revit locks the add-in's DLLs while it runs, so this waits for Revit 2026 to close before copying.
param(
    [int]$TimeoutMinutes = 60,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$src = Join-Path $repo 'plugin\bin\AddIn 2026 Release R26'
$dst = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2026'

if (-not $SkipBuild) {
    # The two projects, not the solution: the test project needs the .NET 10 SDK.
    foreach ($project in 'plugin\RevitMCPPlugin.csproj', 'commandset\RevitMCPCommandSet.csproj') {
        dotnet build (Join-Path $repo $project) -c 'Release R26' -v q -nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
}
if (-not (Test-Path (Join-Path $src 'NPC.MCP.addin'))) { throw "No build output in $src" }

function Get-Revit2026 { Get-Process Revit -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\Revit 2026\*' } }

if (Get-Revit2026) {
    Write-Host "Revit 2026 is open; waiting up to $TimeoutMinutes minutes for it to close..."
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while (Get-Revit2026) {
        if ((Get-Date) -gt $deadline) { Write-Host 'Revit 2026 is still open; nothing installed.'; exit 3 }
        Start-Sleep -Seconds 5
    }
    Start-Sleep -Seconds 3   # let Revit release its file handles
}

Copy-Item (Join-Path $src '*') $dst -Recurse -Force
Get-ChildItem (Join-Path $dst 'revit_mcp_plugin') -Recurse -File | Unblock-File

$files = Get-ChildItem $src -Recurse -File
$mismatches = $files | Where-Object {
    $target = Join-Path $dst $_.FullName.Substring($src.Length)
    -not (Test-Path $target) -or (Get-FileHash $_.FullName).Hash -ne (Get-FileHash $target).Hash
}
if ($mismatches) {
    Write-Host 'Installed, but these files differ from the build:'
    $mismatches | ForEach-Object { $_.FullName.Substring($src.Length) }
    exit 2
}

$commit = git -C $repo log -1 --format='%h %s'
Write-Host "Installed $($files.Count) files from $commit into $dst at $(Get-Date -Format HH:mm:ss); all match the build."
