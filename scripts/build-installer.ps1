[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
# Build from the portable package produced by package.ps1; never compile a second app.
& (Join-Path $PSScriptRoot 'check-package.ps1')
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$zipName = "Tommi-$version-win-x64.zip"
$setupName = "Tommi-$version-Setup.exe"
$zip = Join-Path $root "dist/$zipName"
$setup = Join-Path $root "dist/$setupName"
$hashPath = Join-Path $root 'build/installer-payload.sha256'
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath $hashPath -Encoding ASCII
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$versionSource = Join-Path $root 'build/InstallerVersion.cs'
('[assembly: System.Reflection.AssemblyVersion("' + $version + '.0")]' + "`n" + '[assembly: System.Reflection.AssemblyFileVersion("' + $version + '.0")]') | Set-Content -LiteralPath $versionSource -Encoding UTF8
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warnaserror+', '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll', "/out:$setup", "/resource:$zip,Payload.zip", "/resource:$hashPath,Payload.sha256", ('/resource:' + (Join-Path $root 'VERSION') + ',Version.txt'), ('/win32icon:' + (Join-Path $root 'assets/brand/tomato-focus.ico')), ('/win32manifest:' + (Join-Path $root 'src/Tomato.Focus/app.manifest')), (Join-Path $PSScriptRoot 'installer/Setup.cs'), (Join-Path $root 'src/Tomato.Focus/Infrastructure/Texts.cs'), $versionSource, ('/resource:' + (Join-Path $root 'build/brand-logo.png') + ',Brand.Logo.png'))
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$verify = Start-Process -FilePath $setup -ArgumentList '--verify-payload' -WindowStyle Hidden -Wait -PassThru
if ($verify.ExitCode -ne 0) { throw 'Installer embedded package verification failed.' }
$functional = Start-Process -FilePath (Join-Path $root 'build/Tomato.Verify.exe') -ArgumentList @('--installer-test', ('"' + $setup + '"')) -WindowStyle Hidden -PassThru
if (!$functional.WaitForExit(120000)) {
    $functional.Kill()
    throw 'Installer verification exceeded two minutes. Only its isolated verification process was stopped.'
}
if (Test-Path -LiteralPath (Join-Path $root 'build/installer-results.txt')) { Get-Content -LiteralPath (Join-Path $root 'build/installer-results.txt') -Encoding UTF8 }
if ($functional.ExitCode -ne 0) { throw 'Installer functional verification failed. See build/installer-results.txt.' }
$previewPath = Join-Path $root 'build/installer-preview.png'
$preview = Start-Process -FilePath $setup -ArgumentList @('--render-preview', ('"' + $previewPath + '"')) -WindowStyle Hidden -Wait -PassThru
if ($preview.ExitCode -ne 0 -or !(Test-Path -LiteralPath $previewPath)) { throw 'Installer preview rendering failed.' }
((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash + '  ' + $setupName) | Set-Content -LiteralPath ($setup + '.sha256') -Encoding ASCII
Write-Host "PASS branded installer created and verified: $setup"
