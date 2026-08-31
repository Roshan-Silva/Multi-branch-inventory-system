param([string]$BaseUrl = "http://localhost:5296")

$ErrorActionPreference = "Stop"

$requiredVariables = @(
    "MBI_ADMIN_EMAIL",
    "MBI_ADMIN_PASSWORD",
    "MBI_EMPLOYEE_EMAIL",
    "MBI_EMPLOYEE_PASSWORD",
    "MBI_MANAGER_EMAIL",
    "MBI_MANAGER_PASSWORD",
    "MBI_INVENTORY_EMAIL",
    "MBI_INVENTORY_PASSWORD",
    "MBI_PROCUREMENT_EMAIL",
    "MBI_PROCUREMENT_PASSWORD"
)

$missingVariables = @($requiredVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
})

if ($missingVariables.Count -gt 0) {
    Write-Host "Missing required environment variables:" -ForegroundColor Red
    foreach ($variableName in $missingVariables) {
        Write-Host "- $variableName" -ForegroundColor Red
    }
    exit 1
}

$normalizedBaseUrl = $BaseUrl.TrimEnd("/")
try {
    Invoke-WebRequest -Uri "$normalizedBaseUrl/api/auth/me" -Method Get -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop | Out-Null
}
catch {
    if ($null -eq $_.Exception.Response) {
        Write-Host "[ERROR] API is not reachable at $normalizedBaseUrl" -ForegroundColor Red
        Write-Host "Start MultiBranchInventory.Api before running this script." -ForegroundColor Yellow
        exit 1
    }
}

$scriptDirectory = $PSScriptRoot
$powerShellExecutable = Join-Path $PSHOME "pwsh.exe"
if (-not (Test-Path -LiteralPath $powerShellExecutable)) {
    $powerShellExecutable = (Get-Process -Id $PID).Path
}

$suites = @(
    [pscustomobject]@{ Name = "Category Management"; Script = "test-category-api.ps1" },
    [pscustomobject]@{ Name = "Product Management"; Script = "test-product-api.ps1" },
    [pscustomobject]@{ Name = "Supplier Management"; Script = "test-supplier-api.ps1" },
    [pscustomobject]@{ Name = "Inventory Management"; Script = "test-inventory-api.ps1" },
    [pscustomobject]@{ Name = "Procurement Workflow"; Script = "test-procurement-api.ps1" },
    [pscustomobject]@{ Name = "Goods Receiving"; Script = "test-goods-receiving-api.ps1" }
)

Write-Host "=================================================="
Write-Host "Backend Regression Suite"
Write-Host "=================================================="

$results = [System.Collections.Generic.List[object]]::new()
foreach ($suite in $suites) {
    $suitePath = Join-Path $scriptDirectory $suite.Script
    Write-Host ""
    Write-Host "Running $($suite.Name)..." -ForegroundColor Cyan

    if (-not (Test-Path -LiteralPath $suitePath)) {
        Write-Host "[ERROR] Missing suite: $suitePath" -ForegroundColor Red
        $results.Add([pscustomobject]@{
            Name = $suite.Name
            Passed = $false
            ExitCode = 1
        })
        continue
    }

    & $powerShellExecutable -NoProfile -File $suitePath -BaseUrl $normalizedBaseUrl 2>&1 | Out-Host
    $suiteExitCode = $LASTEXITCODE
    $results.Add([pscustomobject]@{
        Name = $suite.Name
        Passed = ($suiteExitCode -eq 0)
        ExitCode = $suiteExitCode
    })
}

$passedCount = @($results | Where-Object Passed).Count
$failedCount = $results.Count - $passedCount

Write-Host ""
Write-Host "=================================================="
Write-Host "Backend Regression Summary"
Write-Host "=================================================="
foreach ($result in $results) {
    $status = if ($result.Passed) { "PASS" } else { "FAIL" }
    $color = if ($result.Passed) { "Green" } else { "Red" }
    Write-Host (("{0,-28} {1}" -f $result.Name, $status)) -ForegroundColor $color
}

Write-Host ""
Write-Host "Suites passed: $passedCount"
Write-Host "Suites failed: $failedCount"
Write-Host ""

if ($failedCount -eq 0) {
    Write-Host "BACKEND REGRESSION: PASS" -ForegroundColor Green
    exit 0
}

Write-Host "BACKEND REGRESSION: FAIL" -ForegroundColor Red
exit 1
