#Requires -RunAsAdministrator
<#
  E2E LAI GIAO DIEN (ADR-0009, ADR-0010, ADR-0011): tsudisk-gui.exe doi chu cai + nhan o, tao phan vung trong vung
  trong, roi mo rong o, tren O AO VHDX bang UI Automation.
  KHONG BAO GIO cham o that:
   - diskpart chon o bang `select vdisk file=<duong dan VHDX>`, khong chon theo so dia.
   - Giao dien chay voi --chi-o-ao: chi liet ke o VHD/VHDX va RenamePlanner/CreatePartitionPlanner/ExtendPlanner tu choi
     moi o khac (VirtualDiskOnly).
  Chay: CI windows-latest (.github/workflows/ci.yml) va may dev (PowerShell Admin):
     powershell -ExecutionPolicy Bypass -File tests\e2e\run-e2e-gui.ps1 -Exe <duong dan tsudisk-gui.exe>
  Bo cuc (1 GB, GPT): MSR | DATA 512M (nhan GUIE2E, co chu cai) | trong.
  Kiem: (1) chi thay o ao; (2) xac nhan sai -> nut ap dung tat, Huy -> khong doi gi; (3) go dung chu cai -> doi chu cai
  + nhan thanh cong, nhat ky Succeeded, du lieu nguyen ven; (4) giao dien doc lai bo cuc moi; (5) tao phan vung: go sai
  so dia -> nut tat, Huy -> khong doi gi; (6) go dung so dia -> phan vung exFAT 200 MB moi trong vung trong, phan vung cu
  giu nguyen vi tri/kich thuoc/du lieu, ghi duoc tep len o moi, ban do hien o moi; (7) mo rong o bi chan boi phan vung
  khac -> tu choi NotAdjacent, khong co nut ap dung; (8) [NTFS][Recovery khong chua WinRE][trong] -> tu choi
  RecoveryNotWinRe (giao dien khong co "tao lai Recovery rong"); (9) [NTFS][trong]: go sai chu cai, Huy -> khong doi;
  (10) go dung chu cai -> o lon them, vi tri bat dau + du lieu giu nguyen, phan vung khac khong doi; (11) modal khoa
  (--du-lieu-mau --xem-khoa-mau, ADR-0012): dung hai nut, toan man, focus trong modal, Esc/Alt+F4/WM_CLOSE khong dong.
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

# Phan tu dang hien (Collapsed trong WPF => khong co trong cay UIA). Khong doi - dung de kiem "KHONG co nut".
function Test-Shown($Root, [string]$Id) {
    $e = $Root.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, $Id)))
    return ($null -ne $e -and -not $e.Current.IsOffscreen)
}

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
    $apply = Find-ById $main 'ApplyPlanButton'
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

    Write-Host "`n=== Ca G5: tao phan vung - go sai so dia, Huy -> khong doi ==="
    $partsBefore = @(Get-Partition -DiskNumber $disk.Number)
    $freeBlock = Wait-Until { @($map.FindAll($Scope::Children, $listItem)) | Where-Object { $_.Current.ItemStatus -eq 'Free' } | Select-Object -Last 1 } 'khoi vung trong tren ban do' 30
    $freeBlock.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Set-Text (Find-ById $main 'CreateSizeBox') '200'
    $fsBox = Find-ById $main 'CreateFileSystemBox'
    $fsItem = Get-ComboItems $fsBox | Where-Object { $_.Current.Name -eq 'exFAT' } | Select-Object -First 1
    Assert-True ($null -ne $fsItem) 'o he tep co exFAT'
    $fsItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Close-Combo $fsBox
    Set-Text (Find-ById $main 'CreateLabelBox') 'GUI TAO'
    Invoke-Element (Find-ById $main 'PlanCreateButton')
    $apply = Find-ById $main 'ApplyPlanButton'
    Wait-Until { $apply.Current.IsEnabled -and -not $apply.Current.IsOffscreen } 'nut Ap dung cho ke hoach tao' 20 | Out-Null
    Invoke-Element $apply
    $dialog = Find-ConfirmWindow $proc.Id
    $confirmBtn = Find-ById $dialog 'ApplyButton'
    Set-Text (Find-ById $dialog 'Typed') 'TAO'
    Start-Sleep -Milliseconds 300
    Assert-True (-not $confirmBtn.Current.IsEnabled) "go 'TAO' thay vi so dia -> nut van tat"
    Set-Text (Find-ById $dialog 'Typed') "$($disk.Number + 1)"
    Start-Sleep -Milliseconds 300
    Assert-True (-not $confirmBtn.Current.IsEnabled) 'go so dia KHAC -> nut van tat'
    Invoke-Element (Find-ById $dialog 'CancelButton')
    Wait-Until { -not (Test-ConfirmOpen $proc.Id) } 'dong hop thoai' 10 | Out-Null
    Assert-True (@(Get-Partition -DiskNumber $disk.Number).Count -eq $partsBefore.Count) 'Huy -> khong tao phan vung nao'
    Assert-True (@(Get-ChildItem $Logs -Directory).Count -eq 1) 'Huy -> khong tao lan chay moi'

    Write-Host "`n=== Ca G6: go dung so dia -> tao exFAT 200 MB nhan 'GUI TAO' ==="
    Invoke-Element $apply
    $dialog = Find-ConfirmWindow $proc.Id
    $confirmBtn = Find-ById $dialog 'ApplyButton'
    Set-Text (Find-ById $dialog 'Typed') " $($disk.Number) "
    Start-Sleep -Milliseconds 300
    Assert-True ($confirmBtn.Current.IsEnabled) "go so dia $($disk.Number) -> nut xac nhan bat"
    Invoke-Element $confirmBtn
    # Lan chay doi ten truoc da de ItemStatus = Succeeded: doi lan chay THU HAI co nhat ky ket thuc roi moi doc giao dien.
    $journal2 = Wait-Until {
        Get-ChildItem $Logs -Recurse -Filter journal.json | ForEach-Object { Get-Content -Raw -Encoding UTF8 $_.FullName | ConvertFrom-Json } |
            Where-Object { $_.plan.operation -eq 'create-partition' -and $_.state -ne 'Running' } | Select-Object -First 1
    } 'nhat ky tao phan vung ket thuc' 240
    Assert-True ($journal2.state -eq 'Succeeded') "nhat ky: create-partition $($journal2.state)"
    $state = Wait-Until { $s = $result.Current.ItemStatus; if ($s -and $s -ne 'Running') { $s } } 'ket qua lan chay tao' 60
    Assert-True ($state -eq 'Succeeded') 'giao dien bao Succeeded (tao)'
    $partsAfter = @(Get-Partition -DiskNumber $disk.Number)
    Assert-True ($partsAfter.Count -eq $partsBefore.Count + 1) 'them dung mot phan vung'
    $created = $partsAfter | Where-Object { $partsBefore.Offset -notcontains $_.Offset } | Select-Object -First 1
    Assert-True ($null -ne $created -and $created.Size -eq 200MB) 'phan vung moi 200 MB'
    Assert-True ($null -ne $created -and $created.Offset -ge ($data.Offset + $data.Size)) 'phan vung moi nam sau phan vung du lieu (trong vung trong)'
    if ($created) {
        $vol = Get-Volume -Partition $created
        Assert-True ($vol.FileSystem -eq 'exFAT' -and $vol.FileSystemLabel -eq 'GUI TAO') "exFAT nhan 'GUI TAO' (thuc te: $($vol.FileSystem) '$($vol.FileSystemLabel)')"
        Assert-True ([string]$created.DriveLetter -match '^[D-Z]$') "co chu cai $($created.DriveLetter):"
        if ([string]$created.DriveLetter) {
            $probe = Join-Path "$($created.DriveLetter):\" 'probe.txt'
            Set-Content -Path $probe -Value 'tsudisk' -Encoding Ascii
            Assert-True ((Get-Content $probe) -eq 'tsudisk') 'ghi/doc duoc tep tren o moi'
        }
    }
    foreach ($old in $partsBefore) {
        $same = $partsAfter | Where-Object { $_.Offset -eq $old.Offset -and $_.Size -eq $old.Size }
        Assert-True ($null -ne $same) "phan vung cu tai $($old.Offset) giu nguyen vi tri + kich thuoc"
    }
    Assert-True ((Get-FileHash (Join-Path "${New}:\" 'sentinel.bin') -Algorithm SHA256).Hash -eq $hashBefore) 'du lieu o cu nguyen ven sau khi tao (SHA-256 khop)'
    if ($created -and [string]$created.DriveLetter) {
        $createdBlock = Wait-Until { @($map.FindAll($Scope::Children, $listItem)) | Where-Object { $_.Current.Name -match "(^|, )$($created.DriveLetter):(,|$)" } | Select-Object -First 1 } 'khoi o moi tren ban do' 30
        Assert-True ($createdBlock.Current.Name -match 'GUI TAO') 'ban do hien o moi + nhan'
    }

    Write-Host "`n=== Ca G7: mo rong ${New}: bi chan boi phan vung vua tao -> tu choi NotAdjacent ==="
    $script:RefreshMap = { Invoke-Element (Find-ById $main 'RefreshButton'); Start-Sleep -Seconds 2 }
    function Select-Letter([string]$Letter) {
        $b = Wait-Until { @($map.FindAll($Scope::Children, $listItem)) | Where-Object { $_.Current.Name -match "(^|, )${Letter}:(,|$)" } | Select-Object -First 1 } "khoi ${Letter}: tren ban do" 30
        $b.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    function Get-Refusal { Wait-Until { $r = $main.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, 'RefusalTextBlock'))); if ($r -and $r.Current.ItemStatus) { $r.Current.ItemStatus } } 'ma tu choi' 20 }
    Select-Letter $New
    Invoke-Element (Find-ById $main 'PlanExtendButton')
    $code = Get-Refusal
    Assert-True ($code -eq 'NotAdjacent') "tu choi NotAdjacent (thuc te: $code)"
    Assert-True (-not (Test-Shown $main 'ApplyPlanButton')) 'tu choi -> khong co nut ap dung'

    Write-Host "`n=== Ca G8: [NTFS][Recovery khong chua WinRE][trong] -> tu choi RecoveryNotWinRe ==="
    Invoke-Diskpart @(
        "select vdisk file=`"$Vhd`"",
        'create partition primary size=100',
        'format quick fs=ntfs label=GUIMR',
        'assign',
        'create partition primary size=64 id=de94bba4-06d1-4d40-a16a-bfd50179d6ac',
        'gpt attributes=0x8000000000000001'
    ) | Out-Null
    $grow = Get-Partition -DiskNumber $disk.Number | Where-Object { $_.DriveLetter -and $_.Offset -gt $created.Offset } | Select-Object -First 1
    Assert-True ($null -ne $grow) 'diskpart tao o NTFS GUIMR'
    $M = [string]$grow.DriveLetter
    $sentinel2 = Join-Path "${M}:\" 'sentinel2.bin'
    $bytes2 = New-Object byte[] (8MB); (New-Object Random 11).NextBytes($bytes2); [IO.File]::WriteAllBytes($sentinel2, $bytes2)
    $hash2 = (Get-FileHash $sentinel2 -Algorithm SHA256).Hash
    $volBefore = (Get-Volume -Partition $grow).Size
    & $script:RefreshMap
    Select-Letter $M
    Invoke-Element (Find-ById $main 'PlanExtendButton')
    $code = Get-Refusal
    Assert-True ($code -eq 'RecoveryNotWinRe') "tu choi RecoveryNotWinRe (thuc te: $code)"
    Assert-True (-not (Test-Shown $main 'ApplyPlanButton')) 'Recovery khong chua WinRE -> khong co nut ap dung'
    Assert-True (@(Get-ChildItem $Logs -Directory).Count -eq 2) 'tu choi -> khong tao lan chay moi'

    Write-Host "`n=== Ca G9: [NTFS][trong] - go sai chu cai, Huy -> khong doi ==="
    $rec = Get-Partition -DiskNumber $disk.Number | Where-Object { $_.GptType -eq '{de94bba4-06d1-4d40-a16a-bfd50179d6ac}' } | Select-Object -First 1
    Assert-True ($null -ne $rec) 'co phan vung Recovery thu'
    Invoke-Diskpart @("select vdisk file=`"$Vhd`"", "select partition $($rec.PartitionNumber)", 'delete partition override') | Out-Null
    $partsBefore = @(Get-Partition -DiskNumber $disk.Number)
    & $script:RefreshMap
    Wait-Until { -not (@($map.FindAll($Scope::Children, $listItem)) | Where-Object { $_.Current.ItemStatus -eq 'Recovery' }) } 'ban do het khoi Recovery' 30 | Out-Null
    Select-Letter $M
    Invoke-Element (Find-ById $main 'PlanExtendButton')
    $apply = Find-ById $main 'ApplyPlanButton'
    Wait-Until { $apply.Current.IsEnabled -and -not $apply.Current.IsOffscreen } 'nut Ap dung cho ke hoach mo rong' 20 | Out-Null
    Invoke-Element $apply
    $dialog = Find-ConfirmWindow $proc.Id
    $confirmBtn = Find-ById $dialog 'ApplyButton'
    Set-Text (Find-ById $dialog 'Typed') $New
    Start-Sleep -Milliseconds 300
    Assert-True (-not $confirmBtn.Current.IsEnabled) "go chu cai o KHAC (${New}) -> nut van tat"
    Set-Text (Find-ById $dialog 'Typed') 'MO RONG'
    Start-Sleep -Milliseconds 300
    Assert-True (-not $confirmBtn.Current.IsEnabled) "go 'MO RONG' -> nut van tat"
    Invoke-Element (Find-ById $dialog 'CancelButton')
    Wait-Until { -not (Test-ConfirmOpen $proc.Id) } 'dong hop thoai' 10 | Out-Null
    Assert-True ((Get-Data $disk.Number $grow.Offset).Size -eq $grow.Size) 'Huy -> kich thuoc giu nguyen'
    Assert-True (@(Get-ChildItem $Logs -Directory).Count -eq 2) 'Huy -> khong tao lan chay moi'

    Write-Host "`n=== Ca G10: go dung chu cai -> mo rong ${M}: vao vung trong lien ke ==="
    Invoke-Element $apply
    $dialog = Find-ConfirmWindow $proc.Id
    $confirmBtn = Find-ById $dialog 'ApplyButton'
    Set-Text (Find-ById $dialog 'Typed') "$($M.ToLowerInvariant()):"
    Start-Sleep -Milliseconds 300
    Assert-True ($confirmBtn.Current.IsEnabled) "go '$($M.ToLowerInvariant()):' -> nut xac nhan bat"
    Invoke-Element $confirmBtn
    $journal3 = Wait-Until {
        Get-ChildItem $Logs -Recurse -Filter journal.json | ForEach-Object { Get-Content -Raw -Encoding UTF8 $_.FullName | ConvertFrom-Json } |
            Where-Object { $_.plan.operation -eq 'extend' -and $_.state -ne 'Running' } | Select-Object -First 1
    } 'nhat ky mo rong ket thuc' 240
    Assert-True ($journal3.state -eq 'Succeeded') "nhat ky: extend $($journal3.state)"
    $state = Wait-Until { $s = $result.Current.ItemStatus; if ($s -and $s -ne 'Running') { $s } } 'ket qua lan chay mo rong' 60
    Assert-True ($state -eq 'Succeeded') 'giao dien bao Succeeded (mo rong)'
    $grown = Get-Data $disk.Number $grow.Offset
    Write-Host "  ${M}: $($grow.Size) -> $($grown.Size) byte"
    Assert-True ($null -ne $grown -and $grown.Size -ge $grow.Size + 16MB) 'o lon them it nhat 16 MB, cung vi tri bat dau'
    Assert-True ((Get-Volume -Partition $grown).Size -gt $volBefore) 'he tep NTFS lon theo'
    Assert-True ((Get-FileHash $sentinel2 -Algorithm SHA256).Hash -eq $hash2) 'du lieu o mo rong nguyen ven (SHA-256 khop)'
    $partsAfter = @(Get-Partition -DiskNumber $disk.Number)
    Assert-True ($partsAfter.Count -eq $partsBefore.Count) 'so phan vung khong doi'
    foreach ($old in $partsBefore | Where-Object Offset -ne $grow.Offset) {
        $same = $partsAfter | Where-Object { $_.Offset -eq $old.Offset -and $_.Size -eq $old.Size }
        Assert-True ($null -ne $same) "phan vung khac tai $($old.Offset) giu nguyen vi tri + kich thuoc"
    }
    Assert-True ((Get-FileHash (Join-Path "${New}:\" 'sentinel.bin') -Algorithm SHA256).Hash -eq $hashBefore) 'du lieu o dau tien nguyen ven (SHA-256 khop)'
    $probe2 = Join-Path "${M}:\" 'probe2.bin'
    [IO.File]::WriteAllBytes($probe2, (New-Object byte[] (32MB)))
    Assert-True ((Get-Item $probe2).Length -eq 32MB) 'ghi duoc 32 MB len o vua mo rong'

    Write-Host "`n=== Ca G11: modal khoa (che do xem mau) khong dong duoc bang Esc / Alt+F4 / dong cua so ==="
    # Dong ban dang chay (G1-G10) truoc, roi mo ban xem mau: --du-lieu-mau => khong co duong ghi dia; --xem-khoa-mau
    # chi HIEN modal voi phien ban mau (ADR-0012 muc 10), khong di qua cong va khong goi mang.
    try {
        $UIA::FromHandle($proc.MainWindowHandle).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
        if (-not $proc.WaitForExit(15000)) { $proc.Kill() }
    } catch { $proc.Kill() }
    $proc = Start-Process -FilePath $Exe -PassThru -ArgumentList @('--du-lieu-mau', '--xem-khoa-mau', '--thu-muc-nhat-ky', "`"$Logs`"")
    $lockCond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($UIA::ProcessIdProperty, $proc.Id)),
        (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, 'UpdateRequiredWindow')))
    $modal = Wait-Until { $proc.Refresh(); if ($proc.HasExited) { throw "tsudisk-gui thoat som (ma $($proc.ExitCode))" }; $UIA::RootElement.FindFirst($Scope::Descendants, $lockCond) } 'modal khoa' 60
    # Ten UIA cua cua so = tieu de nguyen van (vi hoac en theo ngon ngu he thong) = chu cua tieu de tren modal.
    $heading = (Find-ById $modal 'UpdateRequiredTitle').Current.Name
    Assert-True ($modal.Current.Name -and $modal.Current.Name -eq $heading) "modal co ten UIA = tieu de ('$heading')"
    Assert-True ((Find-ById $modal 'UpdateRequiredBody').Current.Name.Length -gt 40) 'than modal doc duoc bang UIA'
    $buttonCond = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $buttons = @($modal.FindAll($Scope::Descendants, $buttonCond))
    $ids = ($buttons | ForEach-Object { $_.Current.AutomationId } | Sort-Object) -join ','
    Assert-True ($buttons.Count -eq 2 -and $ids -eq 'ExportDataButton,UpdateNowButton') "dung hai nut: $ids"
    $win = $modal.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    Assert-True ($win.Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Maximized) 'modal toan man (Maximized)'
    Assert-True ($win.Current.IsModal) 'cua so la modal'
    $modal.SetFocus()   # tien trinh mo tu nen co the khong duoc Windows cho len truoc; kich hoat roi moi kiem focus
    $focused = Wait-Until { $f = $UIA::FocusedElement; if ($f -and $f.Current.ProcessId -eq $proc.Id) { $f } } 'focus trong modal' 20
    Assert-True ($focused.Current.AutomationId -eq 'UpdateNowButton') "focus ban dau o nut Cap nhat ngay ($($focused.Current.AutomationId))"

    Add-Type -AssemblyName System.Windows.Forms
    function Test-LockAlive { $proc.Refresh(); return (-not $proc.HasExited) -and ($null -ne $UIA::RootElement.FindFirst($Scope::Descendants, $lockCond)) }
    foreach ($key in @(@{ Keys = '{ESC}'; Name = 'Esc' }, @{ Keys = '%{F4}'; Name = 'Alt+F4' })) {
        $modal.SetFocus()
        Start-Sleep -Milliseconds 300
        [System.Windows.Forms.SendKeys]::SendWait($key.Keys)
        Start-Sleep -Milliseconds 1000
        Assert-True (Test-LockAlive) "$($key.Name) khong dong modal"
    }
    try { $win.Close() } catch { Write-Host "  (WindowPattern.Close nem: $($_.Exception.Message))" }
    Start-Sleep -Milliseconds 1000
    Assert-True (Test-LockAlive) 'lenh dong cua so (WM_CLOSE, nhu nut X) khong dong modal'
    # Tab xoay vong trong modal: 4 lan Tab van o mot trong hai nut cua modal.
    for ($i = 0; $i -lt 4; $i++) { [System.Windows.Forms.SendKeys]::SendWait('{TAB}'); Start-Sleep -Milliseconds 200 }
    $f2 = $UIA::FocusedElement
    Assert-True ($f2.Current.ProcessId -eq $proc.Id -and @('UpdateNowButton', 'ExportDataButton') -contains $f2.Current.AutomationId) "Tab xoay vong trong modal ($($f2.Current.AutomationId))"
    $proc.Kill()
    $proc.WaitForExit(15000) | Out-Null
    $proc = $null

    Write-Host "`n=== Ca G12: cua so Tai khoan chi co nut, khong o nhap (AUTH_AND_ACCOUNT 2.1, QR4) ==="
    # May CI chua dang nhap: mo cua so KHONG goi mang, khong tao tep. Khong bam Dang nhap/Mo trang (se mo trinh duyet).
    $proc = Start-Process -FilePath $Exe -PassThru -ArgumentList @('--du-lieu-mau', '--thu-muc-nhat-ky', "`"$Logs`"")
    $mainCond = New-Object System.Windows.Automation.PropertyCondition($UIA::ProcessIdProperty, $proc.Id)
    $main = Wait-Until { $proc.Refresh(); if ($proc.HasExited) { throw "tsudisk-gui thoat som (ma $($proc.ExitCode))" }; $UIA::RootElement.FindFirst($Scope::Children, $mainCond) } 'cua so chinh (G12)' 60
    Invoke-Element (Find-ById $main 'AccountButton')
    $accCond = New-Object System.Windows.Automation.AndCondition($mainCond,
        (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, 'AccountWindow')))
    $account = Wait-Until { $UIA::RootElement.FindFirst($Scope::Descendants, $accCond) } 'cua so Tai khoan' 20
    $editCond = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
    Assert-True ($null -eq $account.FindFirst($Scope::Descendants, $editCond)) 'cua so Tai khoan khong co o nhap (UIA Edit)'
    $buttonCond = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $ids = (@($account.FindAll($Scope::Descendants, $buttonCond)) | ForEach-Object { $_.Current.AutomationId } | Sort-Object) -join ','
    Assert-True ($ids -eq 'CloseButton,DeleteAccountButton,OpenAccountButton,SignInButton') "chua dang nhap: dung bon nut ($ids)"
    Assert-True ((Find-ById $account 'AccountState').Current.Name.Length -gt 20) 'trang thai tai khoan doc duoc bang UIA'
    $account.SetFocus()
    Start-Sleep -Milliseconds 300
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    $closed = Wait-Until { if ($null -eq $UIA::RootElement.FindFirst($Scope::Descendants, $accCond)) { $true } } 'Esc dong cua so Tai khoan' 10
    Assert-True $closed 'Esc dong cua so Tai khoan'
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
