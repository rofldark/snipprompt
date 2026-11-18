# Builds SnipPrompt.exe and the test runner with the C# compiler that ships with Windows (.NET Framework 4.x).
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1 [-Test]
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$csc = "$fw\csc.exe"
$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$lib = "/lib:$fw\WPF"
$refs = 'System.Windows.Forms.dll', 'System.Drawing.dll', 'UIAutomationClient.dll', 'UIAutomationTypes.dll',
        'WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll', 'System.Xaml.dll',
        'System.Runtime.Serialization.dll', 'System.Xml.dll', 'System.Xml.Linq.dll' | ForEach-Object { "/r:$_" }

if ($Test) {
    # Pure logic only: no window, no system access.
    $units = 'Logic.cs', 'Settings.cs', 'Strings.cs' | ForEach-Object { "$root\src\$_" } | Where-Object { Test-Path $_ }
    $tests = Get-ChildItem "$root\tests\*.cs" | ForEach-Object { $_.FullName }
    & $csc /nologo /target:exe /codepage:65001 /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /r:System.Xml.dll /r:System.Xml.Linq.dll "/out:$out\Tests.exe" $units $tests
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$out\Tests.exe"
    exit $LASTEXITCODE
}

$src = Get-ChildItem "$root\src\*.cs" | ForEach-Object { $_.FullName }
$icon = @(); if (Test-Path "$root\icon.ico") { $icon = "/win32icon:$root\icon.ico", "/resource:$root\icon.ico,SnipPrompt.icon.ico" }
& $csc /nologo /target:winexe /optimize+ /codepage:65001 $lib $refs "/win32manifest:$root\app.manifest" $icon "/out:$out\SnipPrompt.exe" $src
exit $LASTEXITCODE
