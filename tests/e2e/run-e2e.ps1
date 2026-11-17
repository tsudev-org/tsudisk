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
$Vhd2 = $null
$Vhd3 = $null
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

    Write-Host "`n=== Ca 9: noi rong Recovery (KB5034441) bang cach thu nho DATA1 ==="
    $hashBefore9 = (Get-FileHash $sentinel -Algorithm SHA256).Hash
    $d1Before = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data1.Offset
    $recBefore = Get-Partition -DiskNumber $disk.Number | Where-Object GptType -eq $RecoveryGuid
    $d2Before = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset
    $r = Invoke-TsuDisk @('ke-hoach-winre', '--dia', "$($disk.Number)", '--chi-o-ao', '--recovery', 'tao-lai-rong', '--recovery-mb', '700')
    Assert-True ($r.Code -eq 0 -and $r.Output -match 'Thu nh') 'ke-hoach-winre lap duoc ke hoach co buoc thu nho'
    $r = Invoke-TsuDisk @('mo-rong-winre', '--dia', "$($disk.Number)", '--chi-o-ao', '--recovery', 'tao-lai-rong', '--recovery-mb', '700', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'mo-rong-winre thanh cong (exit 0)'
    $d1After = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data1.Offset
    $recAfter = Get-Partition -DiskNumber $disk.Number | Where-Object GptType -eq $RecoveryGuid
    $d2After = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset
    $delta = 700MB - $recBefore.Size
    Assert-True ($d1Before.Size - $d1After.Size -eq $delta) "DATA1 nho di dung $($delta / 1MB) MB"
    Assert-True ($recAfter.Size -eq 700MB -and $recAfter.Offset -eq $d1After.Offset + $d1After.Size) 'Recovery moi 700 MB ngay sau DATA1'
    Assert-True ($recAfter.Offset + $recAfter.Size -eq $recBefore.Offset + $recBefore.Size) 'Recovery moi ket thuc dung cho Recovery cu'
    Assert-True ($d2After.Size -eq $d2Before.Size) 'DATA2 khong bi cham'
    Assert-True ((Get-FileHash $sentinel -Algorithm SHA256).Hash -eq $hashBefore9) 'Du lieu DATA2 nguyen ven'
    $r = Invoke-TsuDisk @('kiem-tra-gpt', '--dia', "$($disk.Number)")
    Assert-True ($r.Code -eq 0) 'GPT van toan ven sau khi noi rong Recovery'

    Write-Host "`n=== Ca 10: doi nhan (tieng Viet co dau) + chu cai DATA2 ==="
    $used = @(Get-Volume | Where-Object DriveLetter | ForEach-Object { [string]$_.DriveLetter }) + @(Get-PSDrive -PSProvider FileSystem | ForEach-Object Name)
    $NewLetter = 84..90 | ForEach-Object { [string][char]$_ } | Where-Object { $used -notcontains $_ } | Select-Object -First 1
    # Script luu ASCII (PowerShell 5.1 doc tep khong BOM theo ANSI): dung ma ky tu cho "Du lieu 2" co dau.
    $label = 'D' + [char]0x1EEF + ' li' + [char]0x1EC7 + 'u 2'
    $d2Before10 = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset
    $r = Invoke-TsuDisk @('ke-hoach-doi-ten', $L2, '--chu-cai', $NewLetter, '--nhan', $label, '--chi-o-ao')
    Assert-True ($r.Code -eq 0) "ke-hoach-doi-ten $L2 -> ${NewLetter}: lap duoc ke hoach"
    Assert-True ((Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset).DriveLetter -eq $data2.DriveLetter) 'ke-hoach-doi-ten khong doi gi'
    $r = Invoke-TsuDisk @('doi-ten', $L2, '--chu-cai', $NewLetter, '--nhan', $label, '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'doi-ten thanh cong (exit 0)'
    $d2After10 = Get-Partition -DiskNumber $disk.Number | Where-Object Offset -eq $data2Offset
    Assert-True ([string]$d2After10.DriveLetter -eq $NewLetter) "DATA2 mang chu cai moi ${NewLetter}:"
    Assert-True ((Get-Volume -DriveLetter $NewLetter).FileSystemLabel -eq $label) 'Nhan moi dung tung ky tu (Unicode)'
    Assert-True ($d2After10.Size -eq $d2Before10.Size) 'DATA2 giu nguyen kich thuoc'
    $sentinel = Join-Path "${NewLetter}:\" 'sentinel.bin'
    Assert-True ((Get-FileHash $sentinel -Algorithm SHA256).Hash -eq $hashBefore9) 'Du lieu DATA2 nguyen ven o chu cai moi'
    $r = Invoke-TsuDisk @('doi-ten', "${NewLetter}:", '--chu-cai', $L1, '--chi-o-ao', '--dong-y')
    Assert-True ($r.Code -eq 1 -and $r.Output -match 'LetterInUse') 'tu choi chu cai dang dung (LetterInUse)'
    $r = Invoke-TsuDisk @('ke-hoach-doi-ten', $env:SystemDrive, '--chu-cai', $NewLetter)
    Assert-True ($r.Code -eq 1 -and $r.Output -match 'ProtectedTarget') 'tu choi doi chu cai o he thong (ProtectedTarget)'
    # PowerShell 5.1 bo doi so rong khi goi exe: dung dang --nhan= de xoa nhan.
    $r = Invoke-TsuDisk @('doi-ten', "${NewLetter}:", '--nhan=', '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0 -and [string]::IsNullOrEmpty((Get-Volume -DriveLetter $NewLetter).FileSystemLabel)) 'xoa nhan thanh cong'

    Write-Host "`n=== Ca 11: tao + dinh dang phan vung trong vung trong (o ao thu hai) ==="
    $Vhd2 = Join-Path $WorkDir 'e2e-tao.vhdx'
    Invoke-Diskpart @(
        "create vdisk file=`"$Vhd2`" maximum=1024 type=expandable",
        "select vdisk file=`"$Vhd2`"",
        "attach vdisk",
        "convert gpt",
        "create partition primary size=200",
        "format quick fs=ntfs label=KEEP",
        "assign"
    ) | Out-Null
    $disk2 = Get-DiskImage -ImagePath $Vhd2 | Get-Disk
    $keep = Get-Partition -DiskNumber $disk2.Number | Where-Object DriveLetter
    $keepSentinel = Join-Path "$($keep.DriveLetter):\" 'keep.bin'
    [IO.File]::WriteAllBytes($keepSentinel, $bytes)
    $keepHash = (Get-FileHash $keepSentinel -Algorithm SHA256).Hash
    $n2 = "$($disk2.Number)"
    $r = Invoke-TsuDisk @('ke-hoach-tao-phan-vung', '--chi-o-ao')
    Assert-True ($r.Code -eq 2) 'thieu --dia -> sai cu phap (exit 2), khong tu doan dia'
    $sysDisk = (Get-Disk | Where-Object IsSystem | Select-Object -First 1).Number
    if ($null -ne $sysDisk) {
        $r = Invoke-TsuDisk @('ke-hoach-tao-phan-vung', '--dia', "$sysDisk", '--chi-o-ao')
        Assert-True ($r.Code -eq 1) 'chi-o-ao tu choi dia he thong that'
    }
    $r = Invoke-TsuDisk @('ke-hoach-tao-phan-vung', '--dia', $n2, '--chi-o-ao')
    Assert-True ($r.Code -eq 0) 'ke-hoach-tao-phan-vung lap duoc ke hoach'
    $label2 = 'K' + [char]0x1EF3 + ' ni' + [char]0x1EC7 + 'm'
    $r = Invoke-TsuDisk @('tao-phan-vung', '--dia', $n2, '--kich-thuoc-mb', '300', '--he-tep', 'exFAT', '--nhan', $label2, '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'tao-phan-vung exFAT 300 MB thanh cong (exit 0)'
    $new = Get-Partition -DiskNumber $disk2.Number | Where-Object { $_.Offset -gt $keep.Offset } | Sort-Object Offset | Select-Object -First 1
    Assert-True ($null -ne $new -and $new.Size -eq 300MB -and $new.DriveLetter) "phan vung moi 300 MB co chu cai ($($new.DriveLetter):)"
    Assert-True ($new.Offset -eq $keep.Offset + $keep.Size) 'phan vung moi bat dau ngay sau KEEP'
    $vol = Get-Volume -DriveLetter $new.DriveLetter
    Assert-True ($vol.FileSystem -eq 'exFAT' -and $vol.FileSystemLabel -eq $label2) 'he tep exFAT, nhan Unicode dung'
    $probe = Join-Path "$($new.DriveLetter):\" 'probe.bin'
    [IO.File]::WriteAllBytes($probe, $bytes)
    Assert-True ((Get-FileHash $probe -Algorithm SHA256).Hash -eq $keepHash) 'ghi/doc duoc tren phan vung moi'
    $keepAfter = Get-Partition -DiskNumber $disk2.Number | Where-Object Offset -eq $keep.Offset
    Assert-True ($keepAfter.Size -eq $keep.Size) 'KEEP giu nguyen vi tri va kich thuoc'
    Assert-True ((Get-FileHash $keepSentinel -Algorithm SHA256).Hash -eq $keepHash) 'Du lieu KEEP nguyen ven'
    $r = Invoke-TsuDisk @('tao-phan-vung', '--dia', $n2, '--nhan', 'REST', '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'tao-phan-vung NTFS chiem phan con lai thanh cong'
    $rest = Get-Partition -DiskNumber $disk2.Number | Sort-Object Offset | Select-Object -Last 1
    Assert-True ($rest.DriveLetter -and (Get-Volume -DriveLetter $rest.DriveLetter).FileSystem -eq 'NTFS') 'phan vung cuoi NTFS co chu cai'
    $r = Invoke-TsuDisk @('ke-hoach-tao-phan-vung', '--dia', $n2, '--chi-o-ao')
    Assert-True ($r.Code -eq 1 -and $r.Output -match 'NothingToGain') 'het vung trong -> tu choi NothingToGain'
    $r = Invoke-TsuDisk @('kiem-tra-gpt', '--dia', $n2)
    Assert-True ($r.Code -eq 0) 'GPT o ao thu hai toan ven sau khi tao phan vung'

    Write-Host "`n=== Ca 12: thu nho o NTFS (ADR-0004) roi tao o moi vao vung vua nhuong ==="
    $restL = "$($rest.DriveLetter):"
    $restSentinel = Join-Path "$restL\" 'rest.bin'
    [IO.File]::WriteAllBytes($restSentinel, $bytes)
    $r = Invoke-TsuDisk @('thu-nho', "$($new.DriveLetter):", '--giam-mb', '100', '--chi-o-ao', '--dong-y')
    Assert-True ($r.Code -eq 1 -and $r.Output -match 'UnsupportedFileSystem') 'thu nho exFAT bi tu choi (UnsupportedFileSystem)'
    $r = Invoke-TsuDisk @('thu-nho', $restL, '--giam-mb', '100000', '--chi-o-ao', '--dong-y')
    Assert-True ($r.Code -eq 1) 'thu nho qua muc cho trong bi tu choi'
    $r = Invoke-TsuDisk @('thu-nho', $restL, '--giam-mb', '100', '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'thu-nho REST 100 MB thanh cong (exit 0)'
    $restAfter = Get-Partition -DiskNumber $disk2.Number | Where-Object Offset -eq $rest.Offset
    Assert-True ($rest.Size - $restAfter.Size -eq 100MB) 'REST nho di dung 100 MB, offset giu nguyen'
    Assert-True ((Get-FileHash $restSentinel -Algorithm SHA256).Hash -eq $keepHash) 'Du lieu REST nguyen ven sau khi thu nho'
    $r = Invoke-TsuDisk @('tao-phan-vung', '--dia', $n2, '--nhan', 'TAIL', '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'tao-phan-vung vao vung vua nhuong thanh cong'
    $tail = Get-Partition -DiskNumber $disk2.Number | Where-Object Offset -eq ($restAfter.Offset + $restAfter.Size)
    Assert-True ($null -ne $tail -and $tail.Size -eq 100MB -and $tail.DriveLetter) 'o moi 100 MB nam ngay sau REST'
    $r = Invoke-TsuDisk @('kiem-tra-gpt', '--dia', $n2)
    Assert-True ($r.Code -eq 0) 'GPT van toan ven sau thu nho + tao'

    Write-Host "`n=== Ca 13: kiem-tra-mbr2gpt (chi doc) ==="
    $r = Invoke-TsuDisk @('kiem-tra-mbr2gpt', '--dia', $n2)
    Assert-True ($r.Code -eq 0 -and $r.Output -match 'GPT') 'dia GPT -> khong can chuyen (exit 0)'
    $Vhd3 = Join-Path $WorkDir 'e2e-mbr.vhdx'
    Invoke-Diskpart @(
        "create vdisk file=`"$Vhd3`" maximum=256 type=expandable",
        "select vdisk file=`"$Vhd3`"",
        "attach vdisk",
        "convert mbr",
        "create partition primary size=100",
        "format quick fs=ntfs label=MBRDATA",
        "assign"
    ) | Out-Null
    $disk3 = Get-DiskImage -ImagePath $Vhd3 | Get-Disk
    $before3 = Get-Partition -DiskNumber $disk3.Number | Select-Object Offset, Size, MbrType
    $r = Invoke-TsuDisk @('kiem-tra-mbr2gpt', '--dia', "$($disk3.Number)")
    Assert-True ($r.Code -eq 1) 'dia MBR du lieu (khong co Windows) -> khong dat (exit 1)'
    Assert-True ($r.Output -match 'mbr2gpt') 'co ket luan cua mbr2gpt hoac bao thieu cong cu'
    if (Test-Path "$env:SystemRoot\System32\mbr2gpt.exe") {
        Assert-True ($r.Output -match ', Error') 'in dong Error cua mbr2gpt tu setupact.log (ly do tu choi)'
    }
    $r = Invoke-TsuDisk @('kiem-tra-mbr2gpt', '--dia', "$($disk3.Number)", '--json')
    Assert-True ($r.Output -match '"verdict": "(Fail|ToolMissing)"') 'JSON co verdict'
    $disk3After = Get-Disk -Number $disk3.Number
    $after3 = Get-Partition -DiskNumber $disk3.Number | Select-Object Offset, Size, MbrType
    Assert-True ($disk3After.PartitionStyle -eq 'MBR') 'dia van la MBR (chi doc, khong chuyen)'
    Assert-True ((($before3 | ConvertTo-Json) -eq ($after3 | ConvertTo-Json))) 'bo cuc MBR khong doi'

    Write-Host "`n=== Ca 14: xoa phan vung du lieu (ADR-0005) - du lieu con tren dia, huong dan cuu bang TestDisk ==="
    $tailNow = Get-Partition -DiskNumber $disk2.Number | Where-Object Offset -eq $tail.Offset
    $tL = "$($tailNow.DriveLetter):"
    $tailSentinel = Join-Path "$tL\" 'tail.bin'
    [IO.File]::WriteAllBytes($tailSentinel, $bytes)
    $r = Invoke-TsuDisk @('ke-hoach-xoa', 'C:', '--chi-o-ao')
    Assert-True ($r.Code -eq 1) 'ke-hoach-xoa C: bi tu choi'
    $r = Invoke-TsuDisk @('ke-hoach-xoa', $tL, '--chi-o-ao')
    Assert-True ($r.Code -eq 0 -and $r.Output -match "TestDisk" -and $r.Output -match "sector $($tailNow.Offset / 512) ") 'ke hoach chi cach cuu bang TestDisk dung sector bat dau'
    $r = Invoke-TsuDisk @('xoa-phan-vung', $tL, '--chi-o-ao', '--dong-y', '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 2) '--dong-y khong du de xoa (exit 2)'
    $r = Invoke-TsuDisk @('xoa-phan-vung', $tL, '--chi-o-ao', '--xac-nhan', "$tL SAI", '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 2) 'xac nhan sai -> khong xoa (exit 2)'
    $r = Invoke-TsuDisk @('xoa-phan-vung', $tL, '--chi-o-ao', '--xac-nhan', "$tL TAIL", '--thu-muc-nhat-ky', "$tL\logs")
    Assert-True ($r.Code -eq 2) 'thu muc nhat ky tren o sap xoa -> tu choi'
    $open = [IO.File]::Open($tailSentinel, 'Open', 'Read', 'Read')
    try {
        $r = Invoke-TsuDisk @('xoa-phan-vung', $tL, '--chi-o-ao', '--xac-nhan', "$tL TAIL", '--thu-muc-nhat-ky', $Logs)
        Assert-True ($r.Code -ne 0 -and $r.Output -match ([string][char]0x0111 + 'ang')) "o co tep dang mo -> khong xoa (exit $($r.Code))"
        Assert-True ($null -ne (Get-Partition -DiskNumber $disk2.Number | Where-Object Offset -eq $tail.Offset)) 'TAIL van con'
    } finally { $open.Dispose() }
    $keepBefore = Get-Partition -DiskNumber $disk2.Number | Where-Object { $_.Offset -ne $tail.Offset } | Select-Object Offset, Size
    $r = Invoke-TsuDisk @('xoa-phan-vung', $tL, '--chi-o-ao', '--xac-nhan', "$($tL.ToLower()) TAIL", '--thu-muc-nhat-ky', $Logs)
    Assert-True ($r.Code -eq 0) 'xoa-phan-vung thanh cong (exit 0)'
    Assert-True ($null -eq (Get-Partition -DiskNumber $disk2.Number | Where-Object Offset -eq $tail.Offset)) 'TAIL da bi xoa'
    $keepAfter2 = Get-Partition -DiskNumber $disk2.Number | Select-Object Offset, Size
    Assert-True ((($keepBefore | ConvertTo-Json) -eq ($keepAfter2 | ConvertTo-Json))) 'cac phan vung khac giu nguyen'
    $r = Invoke-TsuDisk @('kiem-tra-gpt', '--dia', $n2)
    Assert-True ($r.Code -eq 0) 'GPT toan ven sau khi xoa'
    # Chan doan (A): sau khi xoa, sector dau cua vung cu con chu ky NTFS khong (xoa chi go muc trong bang phan vung).
    $raw = [IO.File]::Open("\\.\PhysicalDrive$($disk2.Number)", 'Open', 'Read', 'ReadWrite')
    try { $buf = New-Object byte[] 4096; [void]$raw.Seek($tail.Offset, 'Begin'); [void]$raw.Read($buf, 0, 4096) } finally { $raw.Dispose() }
    $oem = [Text.Encoding]::ASCII.GetString($buf, 3, 8)
    Write-Host "  OEM id sau khi xoa: '$oem'"
    Assert-True ($oem -eq 'NTFS    ') 'xoa khong ghi de boot sector cua o (du lieu con tren dia)'

    Write-Host "`n=== Ca 7: doc nhat ky ==="
    $first = (Get-ChildItem $Logs -Recurse -Filter journal.json | Sort-Object FullName | Select-Object -First 1).FullName
    $r = Invoke-TsuDisk @('nhat-ky', $first)
    Assert-True ($r.Code -eq 0) 'nhat-ky doc duoc tep journal.json'
}
finally {
    Write-Host "`n=== Don dep ==="
    if ($Vhd3) { try { Invoke-Diskpart @("select vdisk file=`"$Vhd3`"", 'detach vdisk') | Out-Null } catch { Write-Host "detach 3: $_" } }
    if ($Vhd2) { try { Invoke-Diskpart @("select vdisk file=`"$Vhd2`"", 'detach vdisk') | Out-Null } catch { Write-Host "detach 2: $_" } }
    try { Invoke-Diskpart @("select vdisk file=`"$Vhd`"", 'detach vdisk') | Out-Null } catch { Write-Host "detach: $_" }
    Remove-Item -Recurse -Force $WorkDir -ErrorAction SilentlyContinue
}

if ($script:Failures.Count -gt 0) {
    Write-Host "`nE2E THAT BAI: $($script:Failures.Count) kiem tra" -ForegroundColor Red
    exit 1
}
Write-Host "`nE2E DAT: moi kiem tra deu qua" -ForegroundColor Green
exit 0
