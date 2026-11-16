$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$refs=@((Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName)
$refs += [Drawing.Bitmap].Assembly.Location
$refs += (Get-ChildItem $PSHOME -Filter 'System.Private.Windows*.dll').FullName
Add-Type -Path (Join-Path $PSScriptRoot 'BuildNativeBackground.cs') -ReferencedAssemblies $refs
[BuildNativeBackground]::Build($root)
