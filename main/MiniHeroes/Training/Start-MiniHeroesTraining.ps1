param(
    [string]$RunId = "miniheroes_grunt_" + (Get-Date -Format "yyyyMMdd_HHmmss"),
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.0.62f1\Editor\Unity.exe",
    [string]$ProjectPath = "C:\Users\Arnau\Desktop\Institut\Projectes\tr3-joc-dawtr3gminiheroes\main\MiniHeroes",
    [string]$ConfigPath = "C:\Users\Arnau\Desktop\Institut\Projectes\tr3-joc-dawtr3gminiheroes\main\MiniHeroes\Training\miniheroes_grunt_ppo.yaml",
    [int]$TimeScale = 8
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity no encontrado en: $UnityPath"
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Proyecto no encontrado en: $ProjectPath"
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config no encontrada en: $ConfigPath"
}

$mlAgentsCommand = Get-Command "mlagents-learn" -ErrorAction SilentlyContinue
if ($null -eq $mlAgentsCommand) {
    throw "No encuentro 'mlagents-learn' en PATH. Activa tu entorno Python primero."
}

Write-Host "RunId: $RunId"
Write-Host "Lanzando trainer..."

$trainerCommand = @"
cd /d `"$ProjectPath\Training`"
mlagents-learn `"$ConfigPath`" --run-id `"$RunId`" --time-scale $TimeScale
pause
"@

Start-Process -FilePath "cmd.exe" -ArgumentList "/k", $trainerCommand

Write-Host "Lanzando Unity en modo entrenamiento..."

$unityArguments = @(
    "-projectPath", "`"$ProjectPath`"",
    "-miniheroes-train"
)

Start-Process -FilePath $UnityPath -ArgumentList $unityArguments

Write-Host "Trainer y Unity lanzados."
