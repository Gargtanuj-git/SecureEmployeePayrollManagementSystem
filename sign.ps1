$cert = Get-Item "Cert:\CurrentUser\My\20A935B256F66965A1CEE44744438246AF1DFB32"
$targetDlls = @(
    "SecureEmployeePayrollSystem\bin\Debug\net10.0\SecureEmployeePayrollSystem.dll",
    "SecureEmployeePayrollSystem\bin\Debug\net10.0\SecureEmployeePayrollSystem.exe",
    "SecureEmployeePayrollSystem.Tests\bin\Debug\net10.0\SecureEmployeePayrollSystem.dll",
    "SecureEmployeePayrollSystem.Tests\bin\Debug\net10.0\SecureEmployeePayrollSystem.Tests.dll"
)
foreach ($target in $targetDlls) {
    if (Test-Path $target) {
        Set-AuthenticodeSignature -FilePath $target -Certificate $cert | Out-Null
    }
}
Write-Host "Auto-signed target SEPS assemblies with $($cert.Thumbprint)."
