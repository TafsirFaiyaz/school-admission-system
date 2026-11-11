# Run from the repository root: .\CHECK_CONNECTION.ps1
# This script only reads the running API and database. It never deletes data.
$ErrorActionPreference = 'Stop'

Write-Host "Checking Docker services ..." -ForegroundColor Cyan
docker compose ps

$directUrl = 'http://localhost:8080/classes'
$proxyUrl = 'http://localhost:5173/backend/classes'

try {
    $directJson = (Invoke-WebRequest -Uri $directUrl -UseBasicParsing).Content
    $proxyJson = (Invoke-WebRequest -Uri $proxyUrl -UseBasicParsing).Content
} catch {
    Write-Host "Could not reach both API addresses: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Try: docker compose up --build -d --force-recreate api frontend'
    exit 1
}

$direct = @(ConvertFrom-Json -InputObject $directJson)
$proxied = @(ConvertFrom-Json -InputObject $proxyJson)
$directIds = @($direct | Where-Object { $_ -and $_.id } | ForEach-Object { $_.id } | Sort-Object)
$proxiedIds = @($proxied | Where-Object { $_ -and $_.id } | ForEach-Object { $_.id } | Sort-Object)

Write-Host "Direct backend classes: $($directIds.Count)" -ForegroundColor Cyan
Write-Host "Frontend proxy classes: $($proxiedIds.Count)" -ForegroundColor Cyan
if (($directIds -join '|') -ceq ($proxiedIds -join '|')) {
    Write-Host 'OK: Both routes return the same class IDs.' -ForegroundColor Green
} else {
    Write-Host 'MISMATCH: The frontend proxy and direct backend return different classes.' -ForegroundColor Red
    Write-Host 'Ensure VITE_API_BASE_URL=/backend, then rebuild frontend and API.'
    Write-Host 'Run: docker compose up --build -d --force-recreate api frontend'
}

try {
    $dbClasses = docker compose exec -T db psql -U school_user -d school_admission -Atc 'SELECT COUNT(*) FROM admission_classes;'
    $dbHolds = docker compose exec -T db psql -U school_user -d school_admission -Atc 'SELECT COUNT(*) FROM admission_holds;'
    Write-Host "PostgreSQL rows: classes=$dbClasses; holds=$dbHolds"
} catch {
    Write-Host "Database row check unavailable: $($_.Exception.Message)" -ForegroundColor Yellow
}

$studentId = Read-Host 'Optional: enter student ID to check application references, or press Enter to skip'
if ($studentId) {
    if ($studentId -cnotmatch '^[0-9]{17}$') {
        Write-Host 'Student ID must have exactly 17 digits. No request was sent.' -ForegroundColor Yellow
    } else {
        try {
            $myHolds = @(Invoke-RestMethod -Uri 'http://localhost:8080/me/holds' -Headers @{'X-Student-Id'=$studentId} | Where-Object { $_ -and $_.holdId })
            $missing = @($myHolds | Where-Object { $_.classId -notin $directIds })
            Write-Host "Student applications: $($myHolds.Count)"
            if ($missing.Count -eq 0) {
                Write-Host 'OK: Each application refers to a class in GET /classes.' -ForegroundColor Green
            } else {
                Write-Host 'MISMATCH: At least one student application refers to a class missing from GET /classes.' -ForegroundColor Red
            }
        } catch {
            Write-Host "Unable to check the student applications: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}
Write-Host 'Check complete. If the browser shows old data, press Ctrl+Shift+R.'
