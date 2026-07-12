# Run TestSprite POS smoke (copies POS-specific config + test plan)
$root = Split-Path -Parent $PSScriptRoot
Copy-Item "$root\testsprite_tests\tmp\config_pos.json" "$root\testsprite_tests\tmp\config.json" -Force
Copy-Item "$root\testsprite_tests\tmp\code_summary_pos.yaml" "$root\testsprite_tests\tmp\code_summary.yaml" -Force
Copy-Item "$root\testsprite_tests\testsprite_frontend_test_plan_pos.json" "$root\testsprite_tests\testsprite_frontend_test_plan.json" -Force
Set-Location $root
node "$env:LOCALAPPDATA\npm-cache\_npx\8ddf6bea01b2519d\node_modules\@testsprite\testsprite-mcp\dist\index.js" generateCodeAndExecute
