# Local development only. Does not persist or print credentials.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CoffeeNChill_Project-main/CoffeeNChill_Project-main/CoffeeNChill.Functions/CoffeeNChill.API/CoffeeNChill.API.csproj'
$names = @('Jwt__Key','AuthStorage__ConnectionString','SeedAdmin__Email','SeedAdmin__Password','ASPNETCORE_ENVIRONMENT')
$old = @{}
foreach ($name in $names) { $old[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $bytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $env:Jwt__Key = [Convert]::ToBase64String($bytes)
    $env:AuthStorage__ConnectionString = 'UseDevelopmentStorage=true'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:SeedAdmin__Email = Read-Host 'Admin email (use the same email after restart)'
    $secure = Read-Host 'Admin password (12+ chars, upper/lowercase, digit, symbol)' -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $env:SeedAdmin__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr); $secure.Dispose() }
    dotnet run --project $project --no-launch-profile --urls 'https://localhost:7043'
    if ($LASTEXITCODE -ne 0) { throw 'API exited unsuccessfully. Review the error above.' }
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $old[$name], 'Process') }
}
