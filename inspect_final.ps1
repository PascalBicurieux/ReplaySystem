$ErrorActionPreference = 'Stop'

Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll"

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

Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.AimingDownSightEventArgs") "AimingDownSightEventArgs"
Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.ShootingEventArgs") "ShootingEventArgs"
Dump-Members (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.TogglingFlashlightEventArgs") "TogglingFlashlightEventArgs"

# Look at aiming/flashlight modules in firearm
Write-Host ""
Write-Host "=========== Aim/Flashlight modules ===========" -ForegroundColor Green
foreach ($t in $asmCS.MainModule.GetTypes()) {
    if ($t.FullName -match "InventorySystem.Items.Firearms.Modules" -and $t.Name -match "(Aim|Adjust|Flashlight|FlashlightCo|Toggling)") {
        Write-Host "TYPE: $($t.FullName) : $($t.BaseType)"
    }
}

Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.LinearAdsModule") "LinearAdsModule"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.FlashlightModule") "FlashlightModule"
Dump-Members (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.IAdsModule") "IAdsModule"

# Throwable network handler
Dump-Members (Get-Type $asmCS "InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler") "ThrowableNetworkHandler"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler") "ServerProcessRequest"

# AnimatorReloaderModuleBase.SendRpc the displayed class
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Autosync.SubcomponentBase") "SendRpc"

# Look at any aiming server method
Write-Host ""
Write-Host "=========== Firearm aim-related signatures ===========" -ForegroundColor Green
$fa = Get-Type $asmCS "InventorySystem.Items.Firearms.Firearm"
foreach ($t in $asmCS.MainModule.GetTypes()) {
    if ($t.BaseType -ne $null -and ($t.BaseType.Name -eq "ModuleBase")) {
        foreach ($m in $t.Methods) {
            if ($m.Name -match "ServerSendStatus|ServerSetAds|ServerToggle|SetAds") {
                Write-Host "  $($t.FullName).$($m.Name)"
            }
        }
    }
}

# JailbirdMessageType enum
Write-Host ""
Write-Host "=========== JailbirdMessageType enum ===========" -ForegroundColor Green
$jbm = Get-Type $asmCS "InventorySystem.Items.Jailbird.JailbirdMessageType"
if ($jbm) {
    foreach ($f in $jbm.Fields) {
        if ($f.HasConstant) {
            Write-Host "  $($f.Name) = $($f.Constant)"
        }
    }
}

# MicroHidPhase enum
Write-Host ""
Write-Host "=========== MicroHidPhase enum ===========" -ForegroundColor Green
$mhp = Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.MicroHidPhase"
if ($mhp) {
    foreach ($f in $mhp.Fields) {
        if ($f.HasConstant) {
            Write-Host "  $($f.Name) = $($f.Constant)"
        }
    }
}

# MicroHidFiringMode enum
Write-Host ""
Write-Host "=========== MicroHidFiringMode enum ===========" -ForegroundColor Green
$mhf = Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.MicroHidFiringMode"
if ($mhf) {
    foreach ($f in $mhf.Fields) {
        if ($f.HasConstant) {
            Write-Host "  $($f.Name) = $($f.Constant)"
        }
    }
}

# Check PrimaryFireModeModule.ServerFire IL
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.PrimaryFireModeModule") "ServerFire"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.MicroHID.Modules.ChargeFireModeModule") "ServerFire"

# AutomaticActionModule ServerShoot
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AutomaticActionModule") "ServerShoot"
Dump-Method-IL (Get-Type $asmCS "InventorySystem.Items.Firearms.Modules.AutomaticActionModule") "ServerSendResponse"

# Look at any "ServerThrow"/"NPC" path
Write-Host ""
Write-Host "=========== Search 'IsLocalPlayer/HostHub' in throwable ===========" -ForegroundColor Green
$tt = Get-Type $asmCS "InventorySystem.Items.ThrowableProjectiles.ThrowableItem"
foreach ($m in $tt.Methods) {
    if ($m.HasBody) {
        foreach ($i in $m.Body.Instructions) {
            $s = $i.ToString()
            if ($s -match "(HostHub|IsLocalPlayer|connectionToClient|isServer|isLocalPlayer)") {
                Write-Host "  $($m.Name) -> $s"
            }
        }
    }
}
