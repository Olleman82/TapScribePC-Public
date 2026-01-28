$files = Get-ChildItem -Path 'd:\Appar\wspr-pc' -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch 'bin|obj' }
foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -like '*SelectAudioFile*') {
            Write-Output "$($file.FullName):$($i + 1): $($lines[$i].Trim())"
        }
    }
}
