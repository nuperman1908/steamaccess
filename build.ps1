$ErrorActionPreference = "Stop"
$src = $PSScriptRoot
$out = Split-Path $src -Parent

foreach ($f in "app.ico", "app-off.ico", "app.manifest", "SteamAccess.cs") {
    if (-not (Test-Path "$src\$f")) { throw "Missing file: $src\$f" }
}

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$cscArgs = @(
    "/nologo", "/target:winexe", "/codepage:65001", "/optimize+",
    "/win32icon:$src\app.ico",
    "/win32manifest:$src\app.manifest",
    "/resource:$src\app.ico,app.ico",
    "/resource:$src\app-off.ico,app-off.ico",
    "/r:System.Windows.Forms.dll", "/r:System.Drawing.dll", "/r:System.Management.dll",
    "/out:$out\SteamAccess.exe",
    "$src\SteamAccess.cs"
)
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
"Built $out\SteamAccess.exe"