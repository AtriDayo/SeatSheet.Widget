# Uses the existing classroom test database; does not run migrations or seed data.
$ErrorActionPreference = 'Stop'
$taskContainerName = 'seatsheet-audit-20260929'
$taskInspectText = docker inspect $taskContainerName
if ($LASTEXITCODE -ne 0) { throw 'Existing database container was not found.' }
$taskContainerInfo = ($taskInspectText | ConvertFrom-Json)[0]
if (-not $taskContainerInfo.State.Running) {
    docker start $taskContainerName
    if ($LASTEXITCODE -ne 0) { throw 'Could not start existing database container.' }
}
$taskDatabaseEnv = @{}
foreach ($taskEntry in $taskContainerInfo.Config.Env) {
    $taskPair = $taskEntry.Split('=', 2)
    $taskDatabaseEnv[$taskPair[0]] = $taskPair[1]
}
$taskPreviousUrl = $env:DATABASE_URL
$taskPreviousPort = $env:PORT
$taskPreviousPassword = $env:ADMIN_PASSWORD
try {
    $env:DATABASE_URL = 'postgresql://' + [uri]::EscapeDataString($taskDatabaseEnv['POSTGRES_USER']) + ':' + [uri]::EscapeDataString($taskDatabaseEnv['POSTGRES_PASSWORD']) + '@localhost:55432/' + $taskDatabaseEnv['POSTGRES_DB'] + '?schema=public'
    $env:PORT = '3000'
    $env:ADMIN_PASSWORD = 'widget-local-demo'
    $taskOriginalProject = Join-Path (Split-Path $PSScriptRoot -Parent) '..\SeatSheet'
    Push-Location $taskOriginalProject
    try { node node_modules/tsx/dist/cli.mjs backend/src/server.ts }
    finally { Pop-Location }
} finally {
    $env:DATABASE_URL = $taskPreviousUrl
    $env:PORT = $taskPreviousPort
    $env:ADMIN_PASSWORD = $taskPreviousPassword
}
