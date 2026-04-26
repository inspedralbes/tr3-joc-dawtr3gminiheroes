param(
    [string]$ResultsRoot = "C:\Users\Arnau\Desktop\Institut\Projectes\tr3-joc-dawtr3gminiheroes\main\MiniHeroes\Training\results",
    [string]$DestinationDir = "C:\Users\Arnau\Desktop\Institut\Projectes\tr3-joc-dawtr3gminiheroes\main\MiniHeroes\Assets\Resources\MLAgents",
    [string]$DestinationFileName = "MiniHeroesGrunt.onnx"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ResultsRoot)) {
    throw "No existe la carpeta de resultados: $ResultsRoot"
}

if (-not (Test-Path -LiteralPath $DestinationDir)) {
    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
}

$latestModel = Get-ChildItem -LiteralPath $ResultsRoot -Recurse -File -Filter "*.onnx" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latestModel) {
    throw "No se encontro ningun modelo .onnx en: $ResultsRoot"
}

$destinationPath = Join-Path $DestinationDir $DestinationFileName
Copy-Item -LiteralPath $latestModel.FullName -Destination $destinationPath -Force

Write-Host "Modelo copiado:"
Write-Host "Origen: $($latestModel.FullName)"
Write-Host "Destino: $destinationPath"
Write-Host "Vuelve a Unity para que reimporte el modelo."
