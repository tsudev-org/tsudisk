#Requires -RunAsAdministrator
<#
  Kiem thu dau-cuoi (E2E) cua TsuDisk tren O AO VHDX - KHONG BAO GIO cham o that:
   - Moi lenh diskpart trong script nay chon o bang `select vdisk file=<duong dan VHDX>`, khong chon theo so dia.
   - Moi lenh tsudisk ghi o deu chay voi --chi-o-ao: TsuDisk tu choi moi o khong phai VHD/VHDX.
  Chay: CI windows-latest (.github/workflows/ci.yml) va may dev (PowerShell Admin):
     powershell -ExecutionPolicy Bypass -File tests\e2e\run-e2e.ps1 -Exe <duong dan tsudisk.exe>
  Bo cuc dung len (3 GB, GPT):  EFI 100M | DATA1 1024M | Recovery 300M | trong 800M | DATA2 512M | trong ~ 330M
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$WorkDir = (Join-Path $env:TEMP ("tsudisk-e2e-" + [guid]::NewGuid().ToString('N').Substring(0, 8)))
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$Vhd = Join-Path $WorkDir 'e2e.vhdx'
$Logs = Join-Path $WorkDir 'logs'
$script:Failures = @()
$MiB = 1MB

function Invoke-Diskpart([string[]]$Lines) {
    $file = Join-Path $WorkDir ("dp-" + [guid]::NewGuid().ToString('N').Substring(0, 6) + '.txt')
    Set-Content -Path $file -Value $Lines -Encoding Ascii
    $out = & "$env:SystemRoot\System32\diskpart.exe" /s $file | Out-String
    if ($LASTEXITCODE -ne 0) { throw "diskpart that bai: $out" }
    return $out
}

function Invoke-TsuDisk([string[]]$Arguments) {
    # PowerShell 5.1 bien dong stderr thanh loi khi EAP=Stop: tam ha xuong Continue cho rieng lenh nay.
    $previous = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $out = & $Exe @Arguments 2>&1 | ForEach-Object { "$_" } | Out-String
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previous
    Write-Host ("> tsudisk " + ($Arguments -join ' ') + "  (exit $code)")
    Write-Host $out
    return [pscustomobject]@{ Code = $code; Output = $out }
}

function Assert-True([bool]$Condition, [string]$Message) {
    if ($Condition) { Write-Host "  OK   $Message" -ForegroundColor Green }
    else { Write-Host "  FAIL $Message" -ForegroundColor Red; $script:Failures += $Message }
}

function Get-Layout([int]$DiskNumber) { Get-Partition -DiskNumber $DiskNumber | Sort-Object Offset }

$RecoveryGuid = '{de94bba4-06d1-4d40-a16a-bfd50179d6ac}'
$disk = $null
try {
    Write-Host "=== Dung o ao $Vhd ==="
    Invoke-Diskpart @(
        "create vdisk file=`"$Vhd`" maximum=3072 type=expandable",
        "select vdisk file=`"$Vhd`"",
        "attach vdisk",
        "convert gpt",
        "create partition efi size=100",
        "format quick fs=fat32",
        "create partition primary size=1024",
        "format quick fs=ntfs label=DATA1",
        "assign",
        "create partition primary size=300",
        "format quick fs=ntfs label=Recovery",
        "set id=de94bba4-06d1-4d40-a16a-bfd50179d6ac override",
        "gpt attributes=0x8000000000000001"
    ) | Out-Null
    $disk = Get-DiskImage -ImagePath $Vhd | Get-Disk
    $parts = Get-Layout $disk.Number
    $recovery = $parts | Where-Object GptType -eq $RecoveryGuid
    # DATA2 sau 800 MB trong, tinh theo offset tuyet doi de bo cuc luon dung nhu mo ta.
    $data2Offset = $recovery.Offset + $recovery.Size + 800 * $MiB
    $data2 = New-Partition -DiskNumber $disk.Number -Offset $data2Offset -Size (512 * $MiB) -AssignDriveLetter
    $data2 | Format-Volume -FileSystem NTFS -NewFileSystemLabel DATA2 -Confirm:$false | Out-Null

    $data1 = Get-Layout $disk.Number | Where-Object { $_.DriveLetter -and $_.Offset -lt $recovery.Offset }
    $data2 = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset
    $L1 = "$($data1.DriveLetter):"; $L2 = "$($data2.DriveLetter):"
    Write-Host "Dia $($disk.Number) BusType=$($disk.BusType); DATA1=$L1 DATA2=$L2"

    # Du lieu canh gac: tep ngau nhien tren DATA2, bam truoc/sau.
    $sentinel = Join-Path "$L2\" 'sentinel.bin'
    $bytes = New-Object byte[] (8MB); (New-Object Random 42).NextBytes($bytes); [IO.File]::WriteAllBytes($sentinel, $bytes)
    $hashBefore = (Get-FileHash $sentinel -Algorithm SHA256).Hash
    $data1Before = $data1.Size

    Write-Host "`n=== Ca 1: chi-o-ao tu choi o that ==="
    $r = Invoke-TsuDisk @('ke-hoach', 'C:', '--chi-o-ao')
    Assert-True ($r.Code -eq 1) 'ke-hoach C: --chi-o-ao bi tu choi (exit 1)'

    Write-Host "`n=== Ca 2: Recovery khong chua WinRE -> mac dinh tu choi ==="
    $r = Invoke-TsuDisk @('ke-hoach', $L1, '--chi-o-ao')
    Assert-True ($r.Code -eq 1 -and $r.Output -match 'RecoveryNotWinRe') 'tu choi RecoveryNotWinRe'

    Write-Host "`n=== Ca 3: doi Recovery + mo rong DATA1 ==="
    $r = Invoke-TsuDisk @('mo-rong', $L1, '--chi-o-ao', '--recovery', 'tao-lai-rong', '--recovery-mb', '512', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'mo-rong DATA1 thanh cong (exit 0)'
    $after = Get-Layout $disk.Number
    $d1 = $after | Where-Object Offset -eq $data1.Offset
    $d2 = $after | Where-Object Offset -eq $data2Offset
    $newRec = $after | Where-Object GptType -eq $RecoveryGuid
    $expectedGain = (300 + 800 - 512) * $MiB
    Assert-True ([math]::Abs(($d1.Size - $data1Before) - $expectedGain) -le $MiB) "DATA1 tang dung $($expectedGain / $MiB) MB (that: $(($d1.Size - $data1Before) / $MiB) MB)"
    Assert-True ($null -ne $d2 -and $d2.Size -eq 512 * $MiB) 'DATA2 giu nguyen offset va kich thuoc'
    Assert-True ($newRec.Offset -eq $d1.Offset + $d1.Size -and $newRec.Size -eq 512 * $MiB) 'Recovery moi 512 MB nam ngay sau DATA1'
    Assert-True ((Get-FileHash $sentinel -Algorithm SHA256).Hash -eq $hashBefore) 'Du lieu tren DATA2 nguyen ven (SHA-256 khop)'
    $journalFile = Get-ChildItem $Logs -Recurse -Filter journal.json | Select-Object -First 1
    $journal = Get-Content -Raw -Encoding UTF8 $journalFile.FullName | ConvertFrom-Json
    Assert-True ($journal.state -eq 'Succeeded') 'Nhat ky ghi Succeeded'
    Assert-True (Test-Path (Join-Path $journalFile.DirectoryName 'bcd-backup')) 'Da sao luu BCD'

    Write-Host "`n=== Ca 4: chay thu tren DATA2 khong doi gi ==="
    $snapshot = (Get-Layout $disk.Number | ForEach-Object { "$($_.Offset):$($_.Size)" }) -join ','
    $r = Invoke-TsuDisk @('mo-rong', $L2, '--chi-o-ao', '--chay-thu', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'chay thu thanh cong (exit 0)'
    Assert-True ($snapshot -eq ((Get-Layout $disk.Number | ForEach-Object { "$($_.Offset):$($_.Size)" }) -join ',')) 'bo cuc khong doi sau chay thu'

    Write-Host "`n=== Ca 5: DATA2 mo rong truc tiep vao vung trong cuoi dia ==="
    $r = Invoke-TsuDisk @('mo-rong', $L2, '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'mo-rong DATA2 thanh cong'
    $d2b = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset
    Assert-True ($d2b.Size -gt 512 * $MiB + 300 * $MiB) "DATA2 da lon hon ($([math]::Round($d2b.Size / $MiB)) MB)"
    Assert-True ((Get-FileHash $sentinel -Algorithm SHA256).Hash -eq $hashBefore) 'Du lieu DATA2 van nguyen ven sau khi mo rong'
    $d1b = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data1.Offset
    Assert-True ($d1b.Size -eq $d1.Size) 'DATA1 khong bi cham khi mo rong DATA2'

    Write-Host "`n=== Ca 6: khong con vung trong lien ke -> tu choi ==="
    $r = Invoke-TsuDisk @('ke-hoach', $L1, '--chi-o-ao')
    Assert-True ($r.Code -eq 1 -and $r.Output -match 'NotAdjacent') 'tu choi NotAdjacent'

    Write-Host "`n=== Ca 8: suc khoe, kiem tra GPT, sao luu tho ==="
    $r = Invoke-TsuDisk @('suc-khoe', '--dia', "$($disk.Number)")
    Assert-True ($r.Code -eq 0) 'suc-khoe chay duoc tren o ao'
    $r = Invoke-TsuDisk @('kiem-tra-gpt', '--dia', "$($disk.Number)")
    Assert-True ($r.Code -eq 0) 'kiem-tra-gpt: GPT o ao toan ven va khop Storage API'
    $backupRoot = Join-Path $WorkDir 'sao-luu'
    $r = Invoke-TsuDisk @('sao-luu', '--dia', "$($disk.Number)", '--thu-muc-nhat-ky', $backupRoot)
    Assert-True ($r.Code -eq 0) 'sao-luu thanh cong'
    $head = Get-ChildItem $backupRoot -Recurse -Filter 'gpt-dau-dia*.bin' | Select-Object -First 1
    Assert-True ($null -ne $head -and $head.Length -eq 1MB) 'co tep sector GPT dau dia 1 MiB'
    $sums = Get-ChildItem $backupRoot -Recurse -Filter 'SHA256SUMS-dia*.txt' | Select-Object -First 1
    $line = (Get-Content $sums.FullName | Where-Object { $_ -match 'gpt-dau-dia' } | Select-Object -First 1) -split '\s+'
    Assert-True ($line[0] -eq (Get-FileHash $head.FullName -Algorithm SHA256).Hash.ToLower()) 'SHA256SUMS khop tep sao luu'
    $runHead = Get-ChildItem $Logs -Recurse -Filter 'gpt-dau-dia*.bin' | Select-Object -First 1
    Assert-True ($null -ne $runHead) 'moi lan mo-rong deu tu sao luu sector GPT tho'

    Write-Host "`n=== Ca 7: doc nhat ky ==="
    $first = (Get-ChildItem $Logs -Recurse -Filter journal.json | Sort-Object FullName | Select-Object -First 1).FullName
    $r = Invoke-TsuDisk @('nhat-ky', $first)
    Assert-True ($r.Code -eq 0) 'nhat-ky doc duoc tep journal.json'
}
finally {
    Write-Host "`n=== Don dep ==="
    try { Invoke-Diskpart @("select vdisk file=`"$Vhd`"", 'detach vdisk') | Out-Null } catch { Write-Host "detach: $_" }
    Remove-Item -Recurse -Force $WorkDir -ErrorAction SilentlyContinue
}

if ($script:Failures.Count -gt 0) {
    Write-Host "`nE2E THAT BAI: $($script:Failures.Count) kiem tra" -ForegroundColor Red
    exit 1
}
Write-Host "`nE2E DAT: moi kiem tra deu qua" -ForegroundColor Green
exit 0
