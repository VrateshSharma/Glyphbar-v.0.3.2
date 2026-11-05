# Runs every suite from clean data. Expects a Debug build, a 1920x1080 primary monitor at 100% scale, and the
# Media Player app. Opens windows, moves the pointer for a few seconds, toggles Show Desktop, and briefly launches
# Calculator. The power flow uses the dry-run executor, so nothing shuts down.
$suites = '1-search-power-recovery', '2-widgets-settings', '3-hotkeys-focus-idle', '4-media', '5-pointer', '6-recovery', '8-desktop-widgets', '9-autohide-animation'

# A script that doesn't parse prints no results, so it would otherwise look like a suite with nothing wrong.
foreach ($suite in $suites + 'gb', 'ui') {
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot\$suite.ps1", [ref]$null, [ref]$errors)
    if ($errors.Count) { "FAIL  $suite.ps1 does not parse: $($errors[0].Message)" }
}

$data = Join-Path $env:TEMP 'glyphbar-e2e\gbdata'
$all = foreach ($suite in $suites) {
    Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
    "=== $suite"
    $lines = @(& "$PSScriptRoot\$suite.ps1" 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -match '^(PASS|FAIL|idle CPU)' })
    $lines
    $count = @($lines | Where-Object { $_ -match '^(PASS|FAIL)' }).Count
    "$suite ran $count checks"
    if ($count -eq 0) { "FAIL  $suite ran no checks" }
}
$all
"TOTAL: $(@($all | Where-Object { "$_" -like 'PASS*' }).Count) passed, $(@($all | Where-Object { "$_" -like 'FAIL*' }).Count) failed"
