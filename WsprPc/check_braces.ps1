$level = 0
$lines = Get-Content 'd:\Appar\wspr-pc\WsprPc\MainWindow.xaml.cs'
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    foreach ($c in $line.ToCharArray()) {
        if ($c -eq '{') { $level++ }
        elseif ($c -eq '}') { $level-- }
    }
    if ($level -eq 0 -and $i -ge 27 -and $i -lt ($lines.Count - 1)) {
        Write-Output "Level hit 0 early at line $($i + 1): $line"
        exit
    }
    if ($level -lt 0) {
        Write-Output "Level dropped below 0 at line $($i + 1): $line"
        exit
    }
}
Write-Output "Level at end: $level"
