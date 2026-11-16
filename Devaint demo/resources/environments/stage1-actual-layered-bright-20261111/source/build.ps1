$ErrorActionPreference = 'Stop'
$refs = @((Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName)
$refs += [System.Drawing.Bitmap].Assembly.Location
$refs += (Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll').FullName
Add-Type -Path (Join-Path $PSScriptRoot 'build.cs') -ReferencedAssemblies $refs
[RoomLayers.Build]::Run((Split-Path $PSScriptRoot -Parent))
