<#
.SYNOPSIS
  Compatibility shim — canonical script is scripts/rksv/ensure-bmf-prueftool.ps1
#>
& (Join-Path $PSScriptRoot 'rksv\ensure-bmf-prueftool.ps1') @args
exit $LASTEXITCODE
