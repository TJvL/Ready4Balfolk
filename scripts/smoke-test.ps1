<#
.SYNOPSIS
    Launches Ready4Balfolk with --smoke-test and fails if it did not start cleanly.

.DESCRIPTION
    Used by CI, and the quickest way to reproduce a CI failure locally:

        pwsh scripts/smoke-test.ps1 publish\Ready4Balfolk.UI.exe
        pwsh scripts/smoke-test.ps1 "$env:ProgramFiles\Ready4Balfolk\Ready4Balfolk.UI.exe"

    Windows runners have an interactive session, so unlike Linux there is no virtual display to
    set up here.

.PARAMETER Executable
    The Ready4Balfolk.UI.exe to launch.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Executable)) {
    throw "no executable at $Executable"
}

# Two reasons not to just invoke it and read $LASTEXITCODE. The UI project is a WinExe, so
# PowerShell does not wait for it the way it waits for a console process, and a GUI subsystem
# process started without redirection has nowhere to write the report the smoke test prints.
# Start-Process -Wait with an explicit redirect solves both.
$stdout = New-TemporaryFile
$stderr = New-TemporaryFile

try {
    # The fixtures live next to this script, so callers never have to know where they are.
    $media = Join-Path $PSScriptRoot 'smoke-test-media'

    $process = Start-Process -FilePath $Executable -ArgumentList '--smoke-test', '--smoke-test-media', $media `
        -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdout.FullName -RedirectStandardError $stderr.FullName

    Get-Content -LiteralPath $stdout.FullName | Write-Host
    Get-Content -LiteralPath $stderr.FullName | Write-Host
}
finally {
    Remove-Item -LiteralPath $stdout.FullName, $stderr.FullName -Force -ErrorAction SilentlyContinue
}

if ($process.ExitCode -ne 0) {
    throw "$Executable --smoke-test exited with $($process.ExitCode)"
}

Write-Host "$Executable started cleanly."
