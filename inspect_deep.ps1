$ErrorActionPreference = 'Stop'

Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll"
Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.Rocks.dll"

$exApi = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Exiled.API.dll"
$exEvents = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Exiled.Events.dll"
$asmCSharp = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Assembly-CSharp-Publicized.dll"

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory("C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\")
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $resolver

$asmExApi = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($exApi, $rp)
$asmExEvents = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($exEvents, $rp)
$asmCS = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmCSharp, $rp)

function Get-Type($asm, $fullName) {
    foreach ($t in $asm.MainModule.GetTypes()) {
        if ($t.FullName -eq $fullName) { return $t }
    }
    return $null
}

function Dump-Members($t, [string]$label) {
    if ($t -eq $null) { Write-Host "NOT FOUND: $label" -ForegroundColor Red; return }
    Write-Host ""
    Write-Host "===== $label ===== ($($t.FullName))" -ForegroundColor Cyan
    Write-Host "Base: $($t.BaseType)"
    foreach ($m in $t.Methods | Sort-Object Name) {
        $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        $mods = ""
        if ($m.IsPublic) { $mods += "public " }
        if ($m.IsStatic) { $mods += "static " }
        if ($m.IsVirtual) { $mods += "virtual " }
        Write-Host "  $mods$($m.ReturnType.Name) $($m.Name)($params)"
    }
    foreach ($p in $t.Properties | Sort-Object Name) {
        Write-Host "  prop $($p.PropertyType.Name) $($p.Name)"
    }
    foreach ($f in $t.Fields | Sort-Object Name) {
        if ($f.IsPublic) {
            Write-Host "  fld $($f.FieldType.Name) $($f.Name)"
        }
    }
}

function Dump-Method-IL($t, [string]$methodName) {
    if ($t -eq $null) { return }
    Write-Host ""
    Write-Host "##### IL: $($t.Name).$methodName #####" -ForegroundColor Magenta
    foreach ($m in $t.Methods) {
        if ($m.Name -eq $methodName) {
            $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ', '
            Write-Host "Sig: $($m.ReturnType.FullName) $($m.Name)($params)"
            if ($m.HasBody) {
                foreach ($i in $m.Body.Instructions) {
                    Write-Host "  $($i.ToString())"
                }
            } else {
                Write-Host "  (no body)"
            }
        }
    }
}

# Inspect firearm modules
Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AnimatorReloaderModuleBase") "AnimatorReloaderModuleBase"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.HitscanHitregModuleBase") "HitscanHitregModuleBase"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AutomaticActionModule") "AutomaticActionModule"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.SingleBulletHitscan") "SingleBulletHitscan"

# Look for shoot/fire methods specifically
Write-Host ""
Write-Host "=========== Searching for fire/shoot modules ===========" -ForegroundColor Green
foreach ($t in $asmCS.MainModule.GetTypes()) {
    if ($t.FullName -match "InventorySystem.Items.Firearms.Modules" -and $t.Name -match "(Shoot|Fire|Trigger|Action|Hitscan)") {
        Write-Host "$($t.FullName) : $($t.BaseType)"
    }
}

# AnimatorReloaderModuleBase SendRpc
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AnimatorReloaderModuleBase") "SendRpcHeaderWithRandomByte"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AnimatorReloaderModuleBase") "ServerTryReload"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AnimatorReloaderModuleBase") "ServerProcessCmd"

# AutomaticActionModule fire method
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AutomaticActionModule") "ServerProcessCmd"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AutomaticActionModule") "Fire"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AutomaticActionModule") "ServerCycleAction"

# Throwable ServerThrow / ServerProcessThrowConfirmation
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.ThrowableProjectiles.ThrowableItem") "ServerThrow"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.ThrowableProjectiles.ThrowableItem") "ServerProcessThrowConfirmation"

# Jailbird ServerProcessCmd / UpdateCharging
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Jailbird.JailbirdItem") "ServerProcessCmd"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Jailbird.JailbirdItem") "UpdateCharging"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Jailbird.JailbirdItem") "SendRpc"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Jailbird.JailbirdItem") "JailbirdServerAttack"

# CycleController + InputSyncModule for MicroHID
Dump-Members (Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.CycleController") "CycleController"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.InputSyncModule") "InputSyncModule"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.PrimaryFireModeModule") "PrimaryFireModeModule"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.ChargeFireModeModule") "ChargeFireModeModule"

# Charging events
Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Item.ChargingJailbirdEventArgs") "ChargingJailbirdEventArgs"
Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.ChangingMicroHIDStateEventArgs") "ChangingMicroHIDStateEventArgs"
Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Item.JailbirdChargeCompleteEventArgs") "JailbirdChargeCompleteEventArgs"
Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.AimingEventArgs") "AimingEventArgs"

# Also look for shooting events in exiled
Write-Host ""
Write-Host "=========== EXILED EVENTS: Shooting/Reload/Aim ===========" -ForegroundColor Green
foreach ($t in $asmExEvents.MainModule.GetTypes()) {
    if ($t.FullName -match "EventArgs\..*\.(Shooting|Shot|Reload|Aim|Flashlight|TogglingFlashlight|Charging|Firing)" -and $t.FullName.EndsWith("EventArgs")) {
        Write-Host "$($t.FullName)"
    }
}
