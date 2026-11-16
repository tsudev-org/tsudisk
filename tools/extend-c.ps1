#Requires -RunAsAdministrator
# Extend C: into the unallocated space behind the WinRE Recovery partition, then recreate
# Recovery (1 GB) right after C: and re-enable WinRE. Never touches any other partition (G:, E:, EFI, MSR).
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$Dir = if ($PSScriptRoot) { $PSScriptRoot } else { Join-Path $env:ProgramData 'TsuDisk' }   # logs/backups next to the script
$Ts = Get-Date -Format 'yyyyMMdd-HHmmss'
Start-Transcript -Path "$Dir\run-$Ts.log" | Out-Null

$RecGuid = '{de94bba4-06d1-4d40-a16a-bfd50179d6ac}'
$RecSize = 1024MB
$WinreOnC = 'C:\Windows\System32\Recovery\Winre.wim'

function Snapshot($tag) {
    Get-Partition -DiskNumber 0 | Sort-Object Offset |
        Select-Object PartitionNumber, DriveLetter, Offset, Size, GptType |
        Format-Table -AutoSize | Out-String -Width 250 | Tee-Object -FilePath "$Dir\partitions-$tag-$Ts.txt"
}
function WinreLocation { $o = (& reagentc.exe /info | Out-String); Write-Host $o; if ($o -match 'harddisk(\d+)\\partition(\d+)') { [int]$Matches[2] } else { $null } }
function Run-Diskpart($lines) {
    $f = "$Dir\diskpart-$Ts-$([guid]::NewGuid().ToString('N').Substring(0,6)).txt"
    Set-Content -Path $f -Value $lines -Encoding Ascii
    $out = & diskpart.exe /s $f | Out-String; Write-Host $out
    if ($LASTEXITCODE -ne 0) { throw "diskpart failed (exit $LASTEXITCODE)" }
}

$state = 'start'
try {
    # ---------------- PREFLIGHT (read-only) ----------------
    Write-Host "=== PREFLIGHT ==="
    $disk = Get-Disk -Number 0
    if ($disk.PartitionStyle -ne 'GPT') { throw "Disk 0 is not GPT" }
    $c = Get-Partition -DriveLetter C
    if ($c.DiskNumber -ne 0) { throw "C: is not on disk 0" }
    $parts = @(Get-Partition -DiskNumber 0 | Sort-Object Offset)
    $before = Snapshot 'before'
    $parts | Export-Clixml "$Dir\partitions-before-$Ts.xml"

    $cEnd = $c.Offset + $c.Size
    $rec = $parts | Where-Object { $_.Offset -ge $cEnd } | Select-Object -First 1
    if (-not $rec -or $rec.GptType -ne $RecGuid) { throw "Partition right after C: is not a Recovery partition - abort" }
    if (($rec.Offset - $cEnd) -gt 1MB) { throw "Unexpected gap between C: and Recovery - abort" }
    $next = $parts | Where-Object { $_.Offset -gt $rec.Offset } | Select-Object -First 1
    if (-not $next) { throw "No partition after the free space (layout differs from expected)" }
    $freeEnd = $next.Offset
    $gap = $freeEnd - ($rec.Offset + $rec.Size)
    "Recovery = partition {0} ({1:N0} MB); free gap after it = {2:N2} GB; next partition = {3} ({4}:)" -f `
        $rec.PartitionNumber, ($rec.Size/1MB), ($gap/1GB), $next.PartitionNumber, $next.DriveLetter | Write-Host
    if ($gap -lt 50GB) { throw "Free gap smaller than expected - abort" }

    $newRecOffset = [math]::Floor(($freeEnd - $RecSize) / 1MB) * 1MB
    $newCSize = $newRecOffset - $c.Offset
    "C: {0:N2} GB -> {1:N2} GB; new Recovery at offset {2} size {3:N0} MB" -f ($c.Size/1GB), ($newCSize/1GB), $newRecOffset, (($freeEnd-$newRecOffset)/1MB) | Write-Host

    Write-Host "--- BitLocker"; & manage-bde.exe -status C: | Out-String | Write-Host
    Write-Host "--- Online NTFS scan of C: (read-only)"
    $scan = Repair-Volume -DriveLetter C -Scan
    Write-Host "Scan result: $scan"
    if ("$scan" -ne 'NoErrorsFound') { throw "C: volume scan reported '$scan' - abort, run chkdsk first" }

    & bcdedit.exe /export "$Dir\bcd-backup-$Ts" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "bcdedit export failed" }
    $winrePart = WinreLocation
    "WinRE currently at partition: {0}" -f $winrePart | Write-Host
    if ($null -ne $winrePart -and $winrePart -ne $rec.PartitionNumber) { throw "WinRE lives on another partition ($winrePart) - abort" }
    if ($null -eq $winrePart -and -not (Test-Path $WinreOnC)) { throw "WinRE disabled and Winre.wim not on C: - would lose WinRE image, abort" }

    if ($DryRun) { Write-Host "=== DRY RUN OK - no changes made ==="; return }

    # ---------------- CHANGES ----------------
    Write-Host "=== 1. Disable WinRE (copies Winre.wim to C:) ==="
    if ($null -ne $winrePart) { & reagentc.exe /disable | Out-String | Write-Host; if ($LASTEXITCODE -ne 0) { throw "reagentc /disable failed" } }
    $state = 'winre-disabled'
    if (-not (Test-Path $WinreOnC)) { throw "Winre.wim missing on C: after disable" }
    Copy-Item $WinreOnC "$Dir\Winre-backup-$Ts.wim" -Force

    Write-Host "=== 2. Delete old Recovery partition $($rec.PartitionNumber) ==="
    try { Remove-Partition -DiskNumber 0 -PartitionNumber $rec.PartitionNumber -Confirm:$false }
    catch { Write-Host "Remove-Partition failed ($_), using diskpart override"; Run-Diskpart @('select disk 0', "select partition $($rec.PartitionNumber)", 'delete partition override') }
    $state = 'rec-deleted'

    Write-Host "=== 3. Extend C: ==="
    $sup = Get-PartitionSupportedSize -DriveLetter C
    if ($sup.SizeMax -lt $newCSize) { throw "C: SizeMax ($($sup.SizeMax)) < target ($newCSize)" }
    Resize-Partition -DriveLetter C -Size $newCSize
    $state = 'c-extended'

    Write-Host "=== 4. Create new Recovery partition ==="
    Run-Diskpart @('select disk 0', "create partition primary offset=$($newRecOffset/1KB)", 'format quick fs=ntfs label="Recovery"',
                   "set id=$($RecGuid.Trim('{}')) override", 'gpt attributes=0x8000000000000001')
    $newRec = Get-Partition -DiskNumber 0 | Where-Object { $_.Offset -eq $newRecOffset }
    if (-not $newRec -or $newRec.GptType -ne $RecGuid) { throw "New Recovery partition not found/has wrong type" }
    $state = 'rec-created'

    Write-Host "=== 5. Re-enable WinRE ==="
    & reagentc.exe /enable | Out-String | Write-Host
    $loc = WinreLocation
    if ($loc -ne $newRec.PartitionNumber) { throw "WinRE enabled at partition '$loc', expected $($newRec.PartitionNumber)" }
    $state = 'done'
    Write-Host "=== SUCCESS ==="
}
catch {
    Write-Host "!!! ERROR at state '$state': $_"
    if ($state -eq 'rec-deleted') {
        Write-Host "Rollback: recreate Recovery at its original location"
        try { Run-Diskpart @('select disk 0', "create partition primary offset=$($rec.Offset/1KB) size=$([math]::Floor($rec.Size/1MB))",
                             'format quick fs=ntfs label="Recovery"', "set id=$($RecGuid.Trim('{}')) override", 'gpt attributes=0x8000000000000001') }
        catch { Write-Host "Rollback create failed: $_" }
    }
    if ($state -ne 'start' -and $state -ne 'done') {
        Write-Host "Re-enabling WinRE (works from C: or Recovery)"; & reagentc.exe /enable | Out-String | Write-Host
    }
    $global:failed = $true
}
finally {
    Write-Host "=== AFTER ==="; Snapshot 'after' | Out-Null; Get-Content "$Dir\partitions-after-$Ts.txt" | Write-Host
    Get-Volume -DriveLetter C, G, E | Format-Table DriveLetter, FileSystemLabel, HealthStatus, @{n='SizeGB';e={[math]::Round($_.Size/1GB,2)}}, @{n='FreeGB';e={[math]::Round($_.SizeRemaining/1GB,2)}} -AutoSize | Out-String | Write-Host
    Stop-Transcript | Out-Null
    Set-Content "$Dir\last-run.txt" "run-$Ts.log"
}
