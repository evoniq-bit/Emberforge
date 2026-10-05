$ErrorActionPreference = 'Stop'
$emberSource = $PSScriptRoot
$emberOutput = Split-Path -Parent $emberSource
$emberFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$emberCompiler = Join-Path $emberFramework 'csc.exe'
$emberArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:5',('/out:' + (Join-Path $emberOutput 'Emberforge.exe')),('/win32manifest:' + (Join-Path $emberSource 'Emberforge.manifest')))
foreach ($emberRef in @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')) {
 $emberArgs += '/reference:' + (Join-Path $emberFramework $emberRef)
}
foreach ($emberResource in @('CharacterHooks.json','Deutsch.xaml','English.xaml','MainWindow.xaml','BuildingHooks.json','BuildingCatalog.json','BuildingPages.xaml','BuildingDeutsch.xaml','BuildingEnglish.xaml','FutureRoadmapDeutsch.json','FutureRoadmapEnglish.json')) {
 $emberArgs += '/resource:' + (Join-Path $emberSource $emberResource) + ',' + $emberResource
}
$emberArgs += '/resource:' + (Join-Path $emberOutput 'Emberforge-Logo.png') + ',Emberforge-Logo.png'
Copy-Item -LiteralPath (Join-Path $emberSource 'libzstd.dll') -Destination (Join-Path $emberOutput 'libzstd.dll') -Force
foreach ($emberFile in @('MovementView.cs','MovementNativeTests.cs','AssemblyInfo.cs','Engine.cs','ProgressionEngine.cs','ProgressionView.cs','ProgressionTests.cs','App.cs','SelfTest.cs','BuildingEngine.cs','BuildingNativeTests.cs','BuildingView.cs','BuildingHotkeys.cs','BuildingHotkeyTests.cs','ConsumptionTests.cs','CraftingNativeTests.cs','NearbyTests.cs','DurabilityNativeTests.cs','HeldPickaxeNativeTests.cs','CrosshairMaterialNativeTests.cs','WheelScrolling.cs','ScrollTests.cs','GameCatalog.cs','CatalogTests.cs')) { $emberArgs += Join-Path $emberSource $emberFile }
& $emberCompiler @emberArgs
if ($LASTEXITCODE -ne 0) { throw 'Emberforge konnte nicht kompiliert werden.' }
