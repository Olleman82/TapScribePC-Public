# IconsGeneration.ps1
# This script uses System.Drawing to create colored overlays for the original tapscribe.ico
# and saves them as .ico files for use in the app.

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$baseDir = "d:\Appar\wspr-pc\WsprPc\Assets"
$originalIcoPath = "$baseDir\tapscribe.ico"

function CreateOverlayIcon($originalPath, $outputPath, $color, $opacity) {
    # Load original icon
    $originalIcon = New-Object System.Drawing.Icon($originalPath)
    $bitmap = $originalIcon.ToBitmap()
    
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    
    # Create semi-transparent brush
    $brushColor = [System.Drawing.Color]::FromArgb($opacity, $color.R, $color.G, $color.B)
    $brush = New-Object System.Drawing.SolidBrush($brushColor)
    
    # Draw overlay (keeping some transparency)
    $graphics.FillRectangle($brush, 0, 0, $bitmap.Width, $bitmap.Height)
    
    # Save as PNG first (GDI+ doesn't support direct .ico saving with transparency well)
    $pngPath = $outputPath.Replace(".ico", ".png")
    $bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    
    # Dispose
    $graphics.Dispose()
    $bitmap.Dispose()
    $originalIcon.Dispose()
    
    Write-Host "Generated PNG: $pngPath"
    # Note: For production, we'd convert PNG to ICO. For now, let's assume we can use the PNGs or a tool.
}

# Colors
$listeningGreen = [System.Drawing.Color]::FromArgb(0, 255, 0)
$processingBlue = [System.Drawing.Color]::FromArgb(0, 120, 240)

# Generate
CreateOverlayIcon $originalIcoPath "$baseDir\mic_listening.ico" $listeningGreen 100
CreateOverlayIcon $originalIcoPath "$baseDir\mic_processing.ico" $processingBlue 100
