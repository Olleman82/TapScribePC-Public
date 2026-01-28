$path = 'd:\Appar\wspr-pc\WsprPc\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -like '*SelectAudioFile*') {
        Write-Output "$($i + 1): $($lines[$i])"
    }
}
