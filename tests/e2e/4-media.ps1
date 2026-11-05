# Media selector and control through the real widget. Only ever presses buttons once the owned session is selected.
. "$PSScriptRoot\ui.ps1"
$env:GLYPHBAR_KEEP_OVERLAYS = '1'
$wav = "$sp\silence.wav"
$rate = 8000; $secs = 240; $data = $rate * $secs
$ms = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($ms)
$w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([int](36 + $data)); $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $w.Write([int]16)
$w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$rate); $w.Write([int]$rate); $w.Write([int16]1); $w.Write([int16]8)
$w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([int]$data); $w.Write((New-Object byte[] $data) -as [byte[]])
for ($i = 0; $i -lt 0; $i++) { }
[IO.File]::WriteAllBytes($wav, $ms.ToArray())
$bytes = [IO.File]::ReadAllBytes($wav); for ($i = 44; $i -lt $bytes.Length; $i++) { $bytes[$i] = 128 }; [IO.File]::WriteAllBytes($wav, $bytes)
Start-Process $wav
Start-Sleep -Seconds 8
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 7
Show 'dashboard'
Start-Sleep -Seconds 2
$d = Root 'Glyphbar'
$combo = All $d $CT::ComboBox | Select-Object -First 1
Check 'Session selector shows with two sessions' ($null -ne $combo)
"combo value: $($combo.Current.Name) | patterns: $(($combo.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName -replace 'PatternIdentifiers.Pattern','' }) -join ',')"
$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
Start-Sleep -Milliseconds 900
$items = $combo.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::ListItem)))
"items under combo: $(($items | ForEach-Object { $_.Current.Name }) -join ' | ')"
$pick = $items | Where-Object { $_.Current.Name -match 'Zune' } | Select-Object -First 1
if ($pick) { $pick.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Seconds 1.5 }
$d = Root 'Glyphbar'
$t = Texts $d
$own = ($t -match '^silence$').Count -gt 0
Check 'Selecting a session switches the widget' $own (($t | Where-Object { $_ -match 'silence|ZUNE|CHROME' }) -join ' | ')
if ($own) {
    Press (ByName $d 'Play or pause' $CT::Button)
    Start-Sleep -Seconds 2
    $log1 = (Gb-Log | Select-String 'Media command failed').Count
    Check 'Play/pause button issues the command without error' ($log1 -eq 0)
    Press (ByName (Root 'Glyphbar') 'Play or pause' $CT::Button)
    Start-Sleep -Seconds 1
}
Start-Process $exe -ArgumentList '--exit' -Wait
Start-Sleep -Seconds 3
Check 'Exit is clean' (-not (Gb-Pid)) ([GbWin]::WorkArea())
Stop-Process -Name Microsoft.Media.Player -Force -ErrorAction SilentlyContinue
