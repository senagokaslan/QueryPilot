[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\QueryPilot.Api'
$pgBin = Join-Path $env:ProgramFiles 'PostgreSQL\18\bin'
$pgDump = Join-Path $pgBin 'pg_dump.exe'
$pgRestore = Join-Path $pgBin 'pg_restore.exe'
$psql = Join-Path $pgBin 'psql.exe'

foreach ($executable in @($pgDump, $pgRestore, $psql)) {
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Required PostgreSQL executable was not found: $executable"
    }
}

$secretLines = dotnet user-secrets list --project $apiProject 2>$null

function Get-SecretValue([string]$Key) {
    $line = $secretLines |
        Where-Object { $_ -like "$Key =*" } |
        Select-Object -First 1
    if (-not $line) {
        throw "Required user-secret is missing: $Key"
    }

    return $line.Substring($line.IndexOf('=') + 1).Trim()
}

function Get-ConnectionPart(
    [string]$ConnectionString,
    [string]$Name,
    [string]$Fallback = '') {
    $pattern = "(?i)(?:^|;)\s*$Name\s*=\s*([^;]+)"
    $match = [regex]::Match($ConnectionString, $pattern)
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return $Fallback
}

$appConnection = Get-SecretValue 'ConnectionStrings:DefaultConnection'
$adminPassword = Get-SecretValue 'Development:PostgresAdminPassword'
$hostName = Get-ConnectionPart $appConnection 'Host' '127.0.0.1'
$port = Get-ConnectionPart $appConnection 'Port' '5432'
$sourceDatabase = Get-ConnectionPart $appConnection 'Database'
$appUser = Get-ConnectionPart $appConnection 'Username'
if (-not $appUser) {
    $appUser = Get-ConnectionPart $appConnection 'User ID'
}
$appPassword = Get-ConnectionPart $appConnection 'Password'

$restoreDatabase =
    'querypilot_restore_' + [guid]::NewGuid().ToString('N').Substring(0, 12)
if ($restoreDatabase -notmatch '^querypilot_restore_[a-f0-9]{12}$') {
    throw 'Generated restore database name failed its safety check.'
}

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$backupPath = Join-Path $tempRoot ($restoreDatabase + '.dump')
$resolvedBackupPath = [System.IO.Path]::GetFullPath($backupPath)
if (-not $resolvedBackupPath.StartsWith(
        $tempRoot,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Backup path is outside the operating-system temp directory.'
}

$restoreCreated = $false

try {
    $env:PGPASSWORD = $appPassword
    & $pgDump `
        -h $hostName `
        -p $port `
        -U $appUser `
        -d $sourceDatabase `
        -Fc `
        --no-owner `
        --no-privileges `
        -f $resolvedBackupPath
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
    }

    $sourceOrderCount = (& $psql `
        -h $hostName `
        -p $port `
        -U $appUser `
        -d $sourceDatabase `
        -X `
        -t `
        -A `
        -c 'SELECT COUNT(*) FROM "Orders";').Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Source verification failed with exit code $LASTEXITCODE."
    }

    $env:PGPASSWORD = $adminPassword
    & $psql `
        -h $hostName `
        -p $port `
        -U querypilot_admin `
        -d postgres `
        -X `
        --set ON_ERROR_STOP=1 `
        -c "CREATE DATABASE $restoreDatabase;" |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Restore database creation failed with exit code $LASTEXITCODE."
    }
    $restoreCreated = $true

    & $pgRestore `
        -h $hostName `
        -p $port `
        -U querypilot_admin `
        -d $restoreDatabase `
        --no-owner `
        --no-privileges `
        --exit-on-error `
        $resolvedBackupPath
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore failed with exit code $LASTEXITCODE."
    }

    $restoredOrderCount = (& $psql `
        -h $hostName `
        -p $port `
        -U querypilot_admin `
        -d $restoreDatabase `
        -X `
        -t `
        -A `
        -c 'SELECT COUNT(*) FROM "Orders";').Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Restored data verification failed with exit code $LASTEXITCODE."
    }

    $restoredTableCount = (& $psql `
        -h $hostName `
        -p $port `
        -U querypilot_admin `
        -d $restoreDatabase `
        -X `
        -t `
        -A `
        -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';").Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Restored schema verification failed with exit code $LASTEXITCODE."
    }

    [pscustomobject]@{
        BackupCreated = Test-Path -LiteralPath $resolvedBackupPath
        BackupBytes = (Get-Item -LiteralPath $resolvedBackupPath).Length
        SourceOrders = [int]$sourceOrderCount
        RestoredOrders = [int]$restoredOrderCount
        RestoredTables = [int]$restoredTableCount
        CountsMatch = $sourceOrderCount -eq $restoredOrderCount
    }
}
finally {
    if ($restoreCreated) {
        $env:PGPASSWORD = $adminPassword
        & $psql `
            -h $hostName `
            -p $port `
            -U querypilot_admin `
            -d postgres `
            -X `
            -c "DROP DATABASE $restoreDatabase WITH (FORCE);" |
            Out-Null
    }

    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $resolvedBackupPath) {
        Remove-Item -LiteralPath $resolvedBackupPath -Force
    }

    $appPassword = $null
    $adminPassword = $null
    $appConnection = $null
}
