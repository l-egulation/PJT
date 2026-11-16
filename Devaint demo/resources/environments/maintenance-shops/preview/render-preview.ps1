Add-Type -AssemblyName System.Drawing
$previewRoot = $PSScriptRoot
$layout = Get-Content -LiteralPath (Join-Path $previewRoot 'layout.json') -Raw -Encoding utf8 | ConvertFrom-Json
foreach ($view in $layout.views) {
    $bitmap = New-Object System.Drawing.Bitmap(960,540,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $capture = [System.Drawing.Image]::FromFile((Join-Path $previewRoot $view.capture))
    $graphics.DrawImageUnscaled($capture,0,0)
    $capture.Dispose()
    foreach ($layer in $view.layers) {
        $graphics.InterpolationMode = if ($layer.id -eq 'npc') { [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor } else { [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic }; $asset = $layout.assets.PSObject.Properties[$layer.asset].Value
        $sprite = [System.Drawing.Image]::FromFile((Join-Path $previewRoot $asset.file))
        $b = $asset.bounds
        $target = New-Object System.Drawing.Rectangle([int]$layer.x,[int]$layer.y,[int]$layer.width,[int]$layer.height)
        $graphics.DrawImage($sprite,$target,[int]$b[0],[int]$b[1],[int]($b[2]-$b[0]),[int]($b[3]-$b[1]),[System.Drawing.GraphicsUnit]::Pixel)
        $sprite.Dispose()
    }
    $bitmap.Save((Join-Path $previewRoot ($view.id+'-actual-composite.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}
