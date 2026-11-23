#Requires -RunAsAdministrator
<#
  E2E LAI GIAO DIEN (ADR-0009): tsudisk-gui.exe doi chu cai + nhan o tren O AO VHDX bang UI Automation.
  KHONG BAO GIO cham o that:
   - diskpart chon o bang `select vdisk file=<duong dan VHDX>`, khong chon theo so dia.
   - Giao dien chay voi --chi-o-ao: chi liet ke o VHD/VHDX va RenamePlanner tu choi moi o khac (VirtualDiskOnly).
  Chay: CI windows-latest (.github/workflows/ci.yml) va may dev (PowerShell Admin):
     powershell -ExecutionPolicy Bypass -File tests\e2e\run-e2e-gui.ps1 -Exe <duong dan tsudisk-gui.exe>
  Bo cuc (1 GB, GPT): MSR | DATA 512M (nhan GUIE2E, co chu cai) | trong.
  Kiem: (1) chi thay o ao; (2) xac nhan sai -> nut ap dung tat, Huy -> khong doi gi; (3) go dung chu cai -> doi chu cai
  + nhan thanh cong, nhat ky Succeeded, du lieu nguyen ven; (4) giao dien doc lai bo cuc moi.
  Giao dien chon theo AutomationId (x:Name), khong theo chu - chay duoc o moi ngon ngu he thong.
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$WorkDir = (Join-Path $env:TEMP ("tsudisk-e2e-gui-" + [guid]::NewGuid().ToString('N').Substring(0, 8)))
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$Vhd = Join-Path $WorkDir 'e2e-gui.vhdx'
$Logs = Join-Path $WorkDir 'logs'
$script:Failures = @()
$UIA = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

function Invoke-Diskpart([string[]]$Lines) {
    $file = Join-Path $WorkDir ("dp-" + [guid]::NewGuid().ToString('N').Substring(0, 6) + '.txt')
    Set-Content -Path $file -Value $Lines -Encoding Ascii
    $out = & "$env:SystemRoot\System32\diskpart.exe" /s $file | Out-String
    if ($LASTEXITCODE -ne 0) { throw "diskpart that bai: $out" }
    return $out
}

function Assert-True([bool]$Condition, [string]$Message) {
    if ($Condition) { Write-Host "  OK   $Message" -ForegroundColor Green }
    else { Write-Host "  FAIL $Message" -ForegroundColor Red; $script:Failures += $Message }
}

function Wait-Until([scriptblock]$Probe, [string]$What, [int]$Seconds = 30) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $value = & $Probe
        if ($value) { return $value }
        Start-Sleep -Milliseconds 250
    }
    throw "Het ${Seconds}s cho: $What"
}

function Find-ById($Root, [string]$Id, [int]$Seconds = 30) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, $Id)
    Wait-Until { $Root.FindFirst($Scope::Descendants, $cond) } "AutomationId=$Id" $Seconds
}

function Invoke-Element($Element) {
    $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Set-Text($Element, [string]$Text) {
    $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
}

function Get-ComboItems($Combo) {
    $Combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 300
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = @($Combo.FindAll($Scope::Descendants, $cond))
    return $items
}

function Close-Combo($Combo) {
    $Combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
}

function Find-ConfirmWindow([int]$ProcessId) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($UIA::ProcessIdProperty, $ProcessId)),
        (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, 'ConfirmWindow')))
    # Hop thoai modal co Owner: tuy phien ban Windows nam duoi Desktop hoac duoi cua so chinh.
    Wait-Until { $UIA::RootElement.FindFirst($Scope::Descendants, $cond) } 'hop thoai xac nhan' 20
}

function Test-ConfirmOpen([int]$ProcessId) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($UIA::ProcessIdProperty, $ProcessId)),
        (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, 'ConfirmWindow')))
    return $null -ne $UIA::RootElement.FindFirst($Scope::Descendants, $cond)
}

function Get-Data([int]$DiskNumber, [long]$Offset) { Get-Partition -DiskNumber $DiskNumber | Where-Object Offset -eq $Offset }

$proc = $null
$disk = $null
try {
    Write-Host "=== Dung o ao $Vhd ==="
    Invoke-Diskpart @(
        "create vdisk file=`"$Vhd`" maximum=1024 type=expandable",
        "select vdisk file=`"$Vhd`"",
        "attach vdisk",
        "convert gpt",
        "create partition primary size=512",
        "format quick fs=ntfs label=GUIE2E",
        "assign"
    ) | Out-Null
    $disk = Get-DiskImage -ImagePath $Vhd | Get-Disk
    $data = Get-Partition -DiskNumber $disk.Number | Where-Object DriveLetter | Select-Object -First 1
    $L = [string]$data.DriveLetter
    $used = @((Get-Volume).DriveLetter) + @((Get-PSDrive -PSProvider FileSystem).Name) | Where-Object { $_ } | ForEach-Object { ([string]$_).ToUpperInvariant() }
    $New = (68..90 | ForEach-Object { [string][char]$_ } | Where-Object { $used -notcontains $_ } | Select-Object -Last 1)
    if (-not $New) { throw 'Khong con chu cai trong de thu' }
    Write-Host "Dia $($disk.Number) BusType=$($disk.BusType); DATA=${L}: -> ${New}:"

    $sentinel = Join-Path "${L}:\" 'sentinel.bin'
    $bytes = New-Object byte[] (4MB); (New-Object Random 7).NextBytes($bytes); [IO.File]::WriteAllBytes($sentinel, $bytes)
    $hashBefore = (Get-FileHash $sentinel -Algorithm SHA256).Hash

    Write-Host "`n=== Mo giao dien (--chi-o-ao --dia $($disk.Number)) ==="
    $proc = Start-Process -FilePath $Exe -PassThru -ArgumentList @('--chi-o-ao', '--dia', "$($disk.Number)", '--thu-muc-nhat-ky', "`"$Logs`"")
    $handle = Wait-Until { $proc.Refresh(); if ($proc.HasExited) { throw "tsudisk-gui thoat som (ma $($proc.ExitCode))" }; $proc.MainWindowHandle -ne [IntPtr]::Zero } 'cua so chinh' 60
    $main = $UIA::FromHandle($proc.MainWindowHandle)
    $map = Find-ById $main 'Map'

    Write-Host "`n=== Ca G1: chi thay o ao ==="
    $listItem = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $block = Wait-Until { @($map.FindAll($Scope::Children, $listItem)) | Where-Object { $_.Current.Name -match "(^|, )${L}:(,|$)" } | Select-Object -First 1 } "khoi ${L}: tren ban do" 60
    $diskCombo = Find-ById $main 'DiskBox'
    $diskItems = Get-ComboItems $diskCombo
    Close-Combo $diskCombo
    $virtualCount = @(Get-Disk | Where-Object { $_.BusType -eq 'File Backed Virtual' }).Count
    Write-Host "  dia trong giao dien: $($diskItems.Count); o ao tren may: $virtualCount"
    Assert-True ($diskItems.Count -ge 1 -and $diskItems.Count -le $virtualCount) 'danh sach dia chi gom o ao'

    Write-Host "`n=== Ca G2: lap ke hoach, xac nhan sai, Huy -> khong doi ==="
    $block.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $letterBox = Find-ById $main 'RenameLetterBox'
    $choices = Get-ComboItems $letterBox
    $target = $choices | Where-Object { $_.Current.Name -match "^${New}:?$" } | Select-Object -First 1
    Assert-True ($null -ne $target) "o chu cai co ${New}: (chu cai trong)"
    Assert-True (-not ($choices | Where-Object { $_.Current.Name -match '^[AB]:?$' })) 'khong co A:/B:'
    $target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Close-Combo $letterBox
    Set-Text (Find-ById $main 'RenameLabelBox') 'GUI DOI TEN'
    Invoke-Element (Find-ById $main 'PlanRenameButton')
    $apply = Find-ById $main 'ApplyRenameButton'
    Wait-Until { $apply.Current.IsEnabled } 'nut Ap dung bat (can quyen quan tri)' 20 | Out-Null
    Invoke-Element $apply
    $dialog = Find-ConfirmWindow $proc.Id
    $confirmBtn = Find-ById $dialog 'ApplyButton'
    Assert-True (-not $confirmBtn.Current.IsEnabled) 'chua go gi -> nut xac nhan tat'
    Set-Text (Find-ById $dialog 'Typed') $New
    Start-Sleep -Milliseconds 300
    Assert-True (-not $confirmBtn.Current.IsEnabled) "go chu cai MOI (${New}) thay vi o dang doi -> nut van tat"
    Invoke-Element (Find-ById $dialog 'CancelButton')
    Wait-Until { -not (Test-ConfirmOpen $proc.Id) } 'dong hop thoai' 10 | Out-Null
    $now = Get-Data $disk.Number $data.Offset
    Assert-True ([string]$now.DriveLetter -eq $L -and (Get-Volume -Partition $now).FileSystemLabel -eq 'GUIE2E') 'Huy -> chu cai va nhan giu nguyen'
    Assert-True (-not (Test-Path (Join-Path $Logs '*'))) 'Huy -> khong tao lan chay nao'

    Write-Host "`n=== Ca G3: go dung chu cai -> doi ${L}: thanh ${New}: + nhan ==="
    Invoke-Element $apply
    $dialog = Find-ConfirmWindow $proc.Id
    $confirmBtn = Find-ById $dialog 'ApplyButton'
    Set-Text (Find-ById $dialog 'Typed') "$($L.ToLowerInvariant()):"
    Start-Sleep -Milliseconds 300
    Assert-True ($confirmBtn.Current.IsEnabled) "go '$($L.ToLowerInvariant()):' -> nut xac nhan bat"
    Invoke-Element $confirmBtn
    $result = Find-ById $main 'RunResultText' 120
    $state = Wait-Until { $s = $result.Current.ItemStatus; if ($s -and $s -ne 'Running') { $s } } 'ket qua lan chay' 180
    Write-Host "  ket qua: $state - $($result.Current.Name)"
    Assert-True ($state -eq 'Succeeded') 'giao dien bao Succeeded'
    $after = Get-Data $disk.Number $data.Offset
    Assert-True ([string]$after.DriveLetter -eq $New) "phan vung gio la ${New}:"
    Assert-True ((Get-Volume -Partition $after).FileSystemLabel -eq 'GUI DOI TEN') "nhan gio la 'GUI DOI TEN'"
    Assert-True ($after.Size -eq $data.Size) 'kich thuoc giu nguyen'
    Assert-True ((Get-FileHash (Join-Path "${New}:\" 'sentinel.bin') -Algorithm SHA256).Hash -eq $hashBefore) 'du lieu nguyen ven (SHA-256 khop)'
    $journalFile = Get-ChildItem $Logs -Recurse -Filter journal.json | Select-Object -First 1
    Assert-True ($null -ne $journalFile) 'co journal.json trong --thu-muc-nhat-ky'
    if ($journalFile) {
        $journal = Get-Content -Raw -Encoding UTF8 $journalFile.FullName | ConvertFrom-Json
        Assert-True ($journal.state -eq 'Succeeded' -and $journal.plan.operation -eq 'rename-volume') 'nhat ky: rename-volume Succeeded'
        Assert-True (Test-Path (Join-Path $journalFile.DirectoryName 'plan.json')) 'plan.json ghi truoc khi chay'
    }

    Write-Host "`n=== Ca G4: giao dien doc lai bo cuc moi ==="
    $newBlock = Wait-Until { @($map.FindAll($Scope::Children, $listItem)) | Where-Object { $_.Current.Name -match "(^|, )${New}:(,|$)" } | Select-Object -First 1 } "khoi ${New}: tren ban do" 30
    Assert-True ($null -ne $newBlock -and $newBlock.Current.Name -match 'GUI DOI TEN') 'ban do hien chu cai + nhan moi'
}
catch {
    Write-Host "LOI: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    $script:Failures += "Ngoai le: $_"
}
finally {
    Write-Host "`n=== Don dep ==="
    if ($proc -and -not $proc.HasExited) {
        try {
            $UIA::FromHandle($proc.MainWindowHandle).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
            if (-not $proc.WaitForExit(15000)) { $proc.Kill() }
        } catch { $proc.Kill() }
    }
    try { Invoke-Diskpart @("select vdisk file=`"$Vhd`"", 'detach vdisk') | Out-Null } catch { Write-Host "detach: $_" }
    Remove-Item -Recurse -Force $WorkDir -ErrorAction SilentlyContinue
}

if ($script:Failures.Count -gt 0) {
    Write-Host "`nE2E GIAO DIEN THAT BAI: $($script:Failures.Count) kiem tra" -ForegroundColor Red
    exit 1
}
Write-Host "`nE2E GIAO DIEN DAT: moi kiem tra deu qua" -ForegroundColor Green
exit 0
