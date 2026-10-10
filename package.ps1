# Compila SpawnTracker y crea el zip para Thunderstore en dist/.
# Uso: .\package.ps1   (sube "version_number" en thunderstore\manifest.json antes de cada release)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

dotnet build "$root\SpawnTracker.csproj" -c Release -p:DeployToProfile=false
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló' }

$manifest = Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json
$stage = Join-Path $root 'dist\stage'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null

Copy-Item "$root\thunderstore\manifest.json", "$root\thunderstore\README.md", "$root\thunderstore\CHANGELOG.md", "$root\icon.png" $stage
Copy-Item "$root\bin\Release\SpawnTracker.dll" "$stage\plugins"

$zip = Join-Path $root "dist\$($manifest.name)-$($manifest.version_number).zip"
Remove-Item $zip -ErrorAction SilentlyContinue
# Compress-Archive (PowerShell 5.1) usa "\" en las rutas y Thunderstore las rechaza; se arma a mano con "/".
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry) | Out-Null
    }
} finally { $archive.Dispose() }
Remove-Item $stage -Recurse -Force
Write-Host "Paquete creado: $zip"
