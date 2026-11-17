<#
  Kiem thong tin phien ban nhung trong tsudisk.exe theo .standards/docs/DISTRIBUTION_TRUST.md muc 1.1.4 va
  VERSIONING.md muc 2.2/2.5: CompanyName = tsudev, ProductName, FileDescription, LegalCopyright co gia tri;
  FileVersion = YY.M.DDNN.0; ProductVersion va `tsudisk phien-ban` trung dung chuoi phien ban san pham.
  Ban thu (26.11.1802-beta.1): FileVersion lay phan truoc dau '-' (26.11.1802.0), ProductVersion giu nguyen chuoi.
  Chay: pwsh scripts/kiem-thong-tin-exe.ps1 -Exe publish/tsudisk.exe -Version 26.11.1801
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Version
)
$ErrorActionPreference = 'Stop'
$info = (Get-Item $Exe).VersionInfo
$base = ($Version -split '-')[0]
$loi = @()
if ($info.CompanyName -ne 'tsudev') { $loi += "CompanyName = '$($info.CompanyName)', can 'tsudev'" }
if ($info.ProductName -ne 'TsuDisk') { $loi += "ProductName = '$($info.ProductName)', can 'TsuDisk'" }
if ([string]::IsNullOrWhiteSpace($info.FileDescription) -or $info.FileDescription -eq 'tsudisk') { $loi += "FileDescription chua dat: '$($info.FileDescription)'" }
if ($info.LegalCopyright -notmatch 'tsudev') { $loi += "LegalCopyright = '$($info.LegalCopyright)'" }
if ($info.FileVersion -ne "$base.0") { $loi += "FileVersion = '$($info.FileVersion)', can '$base.0'" }
if ($info.ProductVersion -ne $Version) { $loi += "ProductVersion = '$($info.ProductVersion)', can '$Version'" }
$printed = (& $Exe phien-ban | Out-String)
if ($printed -notmatch [regex]::Escape("TsuDisk $Version")) { $loi += "tsudisk phien-ban in '$($printed.Trim())', can chua 'TsuDisk $Version'" }
$info | Format-List CompanyName, ProductName, FileDescription, LegalCopyright, FileVersion, ProductVersion | Out-String | Write-Host
if ($loi.Count -gt 0) { $loi | ForEach-Object { Write-Host "LOI: $_" -ForegroundColor Red }; exit 1 }
Write-Host 'Thong tin phien ban trong exe: DAT' -ForegroundColor Green
