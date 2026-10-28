# README animation

`RenderPreview.cs` renders the actual Odd2U model into 120 PNG frames without opening the desktop pet or enabling AI or the microphone.

From the repository root in Windows PowerShell:

```powershell
.\build.ps1 -OutputDirectory build-preview
$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& "$fw\csc.exe" /nologo /target:winexe /platform:x64 "/out:$PWD\build-preview\RenderPreview.exe" "/r:$PWD\build-preview\Odd2U.exe" "/r:$fw\System.Xaml.dll" "/r:$fw\WPF\WindowsBase.dll" "/r:$fw\WPF\PresentationCore.dll" "/r:$fw\WPF\PresentationFramework.dll" tools\RenderPreview.cs
Start-Process "$PWD\build-preview\RenderPreview.exe" -ArgumentList ('"' + "$PWD\output\readme-frames" + '"') -WindowStyle Hidden -Wait
python tools\encode_preview.py
```

The encoder requires Pillow. It creates `assets/odd-preview.gif`, a looping eight-second animation at 480 × 480 pixels. Intermediate frames and compiled tools are excluded from Git.
