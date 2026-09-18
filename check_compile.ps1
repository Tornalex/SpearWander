# Simple script to check if Unity project has compilation errors
$unityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Unity.exe"

if (-not (Test-Path $unityPath)) {
    Write-Host "Unity not found at $unityPath, checking alternative path..."
    $unityPath = (Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe" | Select-Object -Last 1).FullName
}

if (Test-Path $unityPath) {
    Write-Host "Found Unity at: $unityPath"
    & $unityPath -projectPath (Get-Location) -logFile - -quit -batchmode -buildTarget StandaloneWindows64 2>&1 | Select-String -Pattern "(error|Error)" | Head -20
} else {
    Write-Host "Could not find Unity executable"
}
