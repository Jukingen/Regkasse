<#
.SYNOPSIS
  Compatibility shim — canonical script is scripts/rksv/verify-rksv-dep-export.ps1
#>
& (Join-Path $PSScriptRoot 'rksv\verify-rksv-dep-export.ps1') @args
exit $LASTEXITCODE
