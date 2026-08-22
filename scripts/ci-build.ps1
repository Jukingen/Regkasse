<#
.SYNOPSIS
  Compatibility shim — canonical script is scripts/ci/ci-build.ps1
#>
& (Join-Path $PSScriptRoot 'ci\ci-build.ps1') @args
exit $LASTEXITCODE
