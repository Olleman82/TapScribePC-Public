$level = 0
$lines = Get-Content 'd:\Appar\wspr-pc\WsprPc\MainWindow.xaml.cs'
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    foreach ($c in $line.ToCharArray()) {
        if ($c -eq '{') { $level++ }
        elseif ($c -eq '}') { $level-- }
    }
    if ($level -eq 0 -and $i -ge 27) {
        Write-Output "Level 0 at line $($i + 1): $line"
    }
}
