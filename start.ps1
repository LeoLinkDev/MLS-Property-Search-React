param(
    [switch]$StopOnly
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$apiProject = Join-Path $root "MLS-Property-Search-API\MLS-Property-Search.csproj"
$frontend = Join-Path $root "MLS-Property-Search-UI"

function Stop-PortProcess([int]$Port) {
    $connections = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $processIds) {
        if ($processId -and $processId -ne $PID) {
            Write-Host "Stopping process $processId on port $Port..."
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

Stop-PortProcess 7030
Stop-PortProcess 5173

if ($StopOnly) {
    Write-Host "MLS API and client stopped."
    exit 0
}

$apiCommand = "Set-Location '$($root.Replace("'", "''"))'; dotnet run --project '$($apiProject.Replace("'", "''"))' --launch-profile https"
$clientCommand = "Set-Location '$($frontend.Replace("'", "''"))'; npm run dev -- --host localhost"

Start-Process powershell.exe -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", $apiCommand
Start-Process powershell.exe -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", $clientCommand

Write-Host "MLS Search API:    https://localhost:7030"
Write-Host "MLS Search Client: http://localhost:5173"
Write-Host "Use .\start.ps1 -StopOnly to stop both services."