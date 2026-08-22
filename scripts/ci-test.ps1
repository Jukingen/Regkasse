<#
.SYNOPSIS
  Compatibility shim — canonical script is scripts/ci/ci-test.ps1
#>
& (Join-Path $PSScriptRoot 'ci\ci-test.ps1') @args
exit $LASTEXITCODE
