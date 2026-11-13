function Get-DevMailConfig {
    $configPath = Join-Path $PSScriptRoot "dev-mail.local.env"
    $examplePath = Join-Path $PSScriptRoot "dev-mail.local.env.example"

    $config = [ordered]@{
        DefaultTestEmail = ""
        BaseUrl          = "http://localhost:5184"
    }

    $sourcePath = if (Test-Path $configPath) { $configPath } elseif (Test-Path $examplePath) { $examplePath } else { $null }
    if ($null -eq $sourcePath) {
        return [pscustomobject]$config
    }

    Get-Content -Path $sourcePath | ForEach-Object {
        $line = $_.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith("#")) { return }

        $eq = $line.IndexOf("=")
        if ($eq -lt 1) { return }

        $key = $line.Substring(0, $eq).Trim()
        $value = $line.Substring($eq + 1).Trim()
        if ($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        switch ($key.ToUpperInvariant()) {
            "DEFAULT_TEST_EMAIL" { $config.DefaultTestEmail = $value }
            "BASE_URL" { $config.BaseUrl = $value }
        }
    }

    return [pscustomobject]$config
}
