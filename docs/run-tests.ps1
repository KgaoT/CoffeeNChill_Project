# Runs the canonical Part 2 Postman collection against a running stack
# (docker compose up -d, or func start --port 7071) via newman.
#
# Split into two newman invocations with a real sleep between them rather
# than a delay inside the collection's own pre-request scripts - see
# run-tests.sh's header comment for why (a busy-wait inside newman's own
# process measurably starved the containers of CPU on this machine).
# "Orders: Status Transitions" has its own short, self-contained
# retry-on-404 poll loop instead, so it doesn't need a shell-level wait.
#
# Usage: .\run-tests.ps1 [-BaseUrl http://localhost:7071]
param(
    [string]$BaseUrl = "http://localhost:7071"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$EnvFile = "CoffeeNChill-Part2.postman_environment.json"
$WorkEnv = Join-Path $env:TEMP "coffeenchill-part2-env-$([guid]::NewGuid()).json"

try {
    $env = Get-Content $EnvFile -Raw | ConvertFrom-Json
    foreach ($v in $env.values) {
        if ($v.key -eq "baseUrl") { $v.value = $BaseUrl }
    }
    $env | ConvertTo-Json -Depth 10 | Set-Content $WorkEnv -Encoding utf8

    Write-Host "== Pass 1/2: Menu, Staff Documents, Orders: Queue, Orders: Status, Orders: Status Transitions =="
    newman run CoffeeNChill-Part2.postman_collection.json `
        -e $WorkEnv --export-environment $WorkEnv `
        --folder "Menu" --folder "Staff Documents" --folder "Orders: Queue" `
        --folder "Orders: Status" --folder "Orders: Status Transitions"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "== Waiting 10s for the main order's lifecycle to reach Collected =="
    Start-Sleep -Seconds 10

    Write-Host "== Pass 2/2: Orders: Processing =="
    newman run CoffeeNChill-Part2.postman_collection.json `
        -e $WorkEnv `
        --folder "Orders: Processing"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "All passes completed successfully."
}
finally {
    Remove-Item -Path $WorkEnv -ErrorAction SilentlyContinue
}
