[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\QueryPilot.Api'
$psql = Join-Path $env:ProgramFiles 'PostgreSQL\18\bin\psql.exe'

if (-not (Test-Path -LiteralPath $psql)) {
    throw "PostgreSQL 18 psql executable was not found at $psql."
}

$adminSecret = dotnet user-secrets list --project $apiProject |
    Where-Object { $_ -like 'Development:PostgresAdminPassword = *' } |
    Select-Object -First 1
if (-not $adminSecret) {
    throw 'Development:PostgresAdminPassword is missing from .NET user-secrets.'
}

$separator = ' = '
$adminPassword = $adminSecret.Substring(
    $adminSecret.IndexOf($separator) + $separator.Length)
$testPassword = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$roleSqlTemplate = @'
DO $role_setup$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'querypilot_test_admin') THEN
        CREATE ROLE querypilot_test_admin
            LOGIN CREATEDB NOSUPERUSER NOCREATEROLE NOREPLICATION
            PASSWORD '__TEST_PASSWORD__';
    ELSE
        ALTER ROLE querypilot_test_admin WITH
            LOGIN CREATEDB NOSUPERUSER NOCREATEROLE NOREPLICATION
            PASSWORD '__TEST_PASSWORD__';
    END IF;
END
$role_setup$;
'@
$roleSql = $roleSqlTemplate.Replace('__TEST_PASSWORD__', $testPassword)

try {
    $env:PGPASSWORD = $adminPassword
    $roleSql | & $psql `
        -h 127.0.0.1 `
        -p 5432 `
        -U querypilot_admin `
        -d postgres `
        -X `
        --set ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL role setup failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    $adminPassword = $null
}

$testConnection = 'Host=127.0.0.1;Port=5432;Database=postgres;' +
    "Username=querypilot_test_admin;Password=$testPassword;SSL Mode=Disable"
dotnet user-secrets set `
    'IntegrationTests:AdminConnectionString' `
    $testConnection `
    --project $apiProject | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Saving the integration test user-secret failed with exit code $LASTEXITCODE."
}

$testPassword = $null
$testConnection = $null
Write-Output 'Integration PostgreSQL role and user-secret are ready.'
