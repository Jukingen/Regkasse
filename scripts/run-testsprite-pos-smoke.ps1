# Run TestSprite POS smoke (copies POS-specific config + test plan)
# Requires: Node.js, network access for npx, TestSprite MCP package.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$posConfig = Join-Path $root 'testsprite_tests\tmp\config_pos.json'
$posSummary = Join-Path $root 'testsprite_tests\tmp\code_summary_pos.yaml'
$posPlan = Join-Path $root 'testsprite_tests\testsprite_frontend_test_plan_pos.json'

foreach ($p in @($posConfig, $posSummary, $posPlan)) {
  if (-not (Test-Path $p)) {
    Write-Error "Missing required file: $p"
  }
}

Copy-Item $posConfig (Join-Path $root 'testsprite_tests\tmp\config.json') -Force
Copy-Item $posSummary (Join-Path $root 'testsprite_tests\tmp\code_summary.yaml') -Force
Copy-Item $posPlan (Join-Path $root 'testsprite_tests\testsprite_frontend_test_plan.json') -Force

Set-Location $root
# Prefer npx (portable) over a machine-specific npm-cache path.
npx --yes @testsprite/testsprite-mcp generateCodeAndExecute
