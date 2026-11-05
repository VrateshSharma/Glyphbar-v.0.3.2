# Runs every suite from clean data. Expects a Debug build, a 1920x1080 primary monitor at 100% scale, and the
# Media Player app. Opens windows, moves the pointer for a few seconds, and briefly launches Calculator.
# The power flow uses the dry-run executor, so nothing shuts down.
$data = Join-Path $env:TEMP 'glyphbar-e2e\gbdata'
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
$all = foreach ($suite in '1-search-power-recovery', '2-widgets-settings', '3-hotkeys-focus-idle', '4-media', '5-pointer', '6-recovery', '8-desktop-widgets') {
    "=== $suite"
    & "$PSScriptRoot\$suite.ps1" 2>&1 | Where-Object { "$_" -match '^(PASS|FAIL|idle CPU|===)' }
}
$all
"TOTAL: $(@($all | Where-Object { "$_" -like 'PASS*' }).Count) passed, $(@($all | Where-Object { "$_" -like 'FAIL*' }).Count) failed"
