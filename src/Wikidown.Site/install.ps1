# Installs the wikidown CLI. If a .NET runtime is already on PATH, uses the
# small NuGet global-tool package (dotnet tool install -g Wikidown.Cli). If
# not, downloads a self-contained single-file binary from this repo's
# GitHub Releases -- no .NET installation required either way.
#
# Usage: irm https://wikidown.org/install.ps1 | iex
#
# Wrapped in a script block so `return` below works the same whether this
# runs as a .ps1 file or is piped through `iex` at an interactive prompt.
& {
$ErrorActionPreference = 'Stop'

$Repo = 'markdav-is/Wikidown'
$InstallDir = if ($env:WIKIDOWN_INSTALL_DIR) { $env:WIKIDOWN_INSTALL_DIR } else { Join-Path $env:USERPROFILE '.wikidown\bin' }

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host "Found dotnet -- installing Wikidown.Cli as a global tool..."
    # dotnet tool install already no-ops cleanly (exit 0) if it's installed
    # -- no update-fallback needed here; `dotnet tool update` is the
    # documented, separate way to move to a newer version.
    dotnet tool install -g Wikidown.Cli
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool install failed" }
    Write-Host "Installed. Run 'wikidown init' to set up this repo."
    return
}

Write-Host "No dotnet found -- installing a self-contained wikidown binary..."

# -- resolve RID -----------------------------------------------------------
$arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
$cpu = switch ($arch) {
    'X64'   { 'x64' }
    'Arm64' { 'arm64' }
    default { throw "unsupported architecture '$arch' -- install the .NET SDK and run: dotnet tool install -g Wikidown.Cli" }
}
$rid = "win-$cpu"
$asset = "wikidown-$rid.zip"

# -- find the newest cli-v* release (GitHub's own "latest" endpoint mixes
#    in this repo's other release tracks, e.g. vsix-v*, so it can't be
#    trusted here) ----------------------------------------------------------
Write-Host "Resolving latest release for $rid..."
$headers = @{ 'User-Agent' = 'wikidown-install-script' }
$releases = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Repo/releases"
$release = $releases | Where-Object { $_.tag_name -like 'cli-v*' } | Select-Object -First 1
if (-not $release) { throw "couldn't find a cli-v* release on GitHub" }

$downloadAsset = $release.assets | Where-Object { $_.name -eq $asset } | Select-Object -First 1
if (-not $downloadAsset) { throw "release $($release.tag_name) has no asset named $asset" }

# -- download + extract -----------------------------------------------------
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
    $zipPath = Join-Path $tmp $asset
    Write-Host "Downloading $($release.tag_name) ($rid)..."
    Invoke-WebRequest -Headers $headers -Uri $downloadAsset.browser_download_url -OutFile $zipPath

    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $InstallDir -Force

    # Strips the zone-identifier mark-of-the-web so SmartScreen doesn't
    # flag the binary the first time it runs.
    Unblock-File -Path (Join-Path $InstallDir 'wikidown.exe')
} finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

# -- add to PATH --------------------------------------------------------
$userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
if (($userPath -split ';') -notcontains $InstallDir) {
    [Environment]::SetEnvironmentVariable('PATH', "$userPath;$InstallDir", 'User')
    Write-Host "Added $InstallDir to your user PATH (new terminals will pick it up)."
}
if (($env:PATH -split ';') -notcontains $InstallDir) {
    $env:PATH = "$env:PATH;$InstallDir"
}

Write-Host "Installed wikidown ($($release.tag_name), $rid) to $InstallDir"
Write-Host "Run 'wikidown init' to set up this repo."
}
