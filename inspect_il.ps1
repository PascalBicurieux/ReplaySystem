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

function Dump-Method-IL($t, [string]$methodName, [int]$paramCount = -1) {
    if ($t -eq $null) { Write-Host "Type null"; return }
    Write-Host ""
    Write-Host "##### IL: $($t.FullName).$methodName #####" -ForegroundColor Magenta
    foreach ($m in $t.Methods) {
        if ($m.Name -eq $methodName) {
            if ($paramCount -ge 0 -and $m.Parameters.Count -ne $paramCount) { continue }
            $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ', '
            Write-Host "Sig: $($m.ReturnType.FullName) $($m.Name)($params)"
            if ($m.HasBody) {
                foreach ($i in $m.Body.Instructions) {
                    Write-Host "  $($i.ToString())"
                }
            } else {
                Write-Host "  (no body / abstract)"
            }
        }
    }
}

# Decompile Exiled wrapper methods
Write-Host "=========== EXILED FIREARM.Reload IL ===========" -ForegroundColor Green
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Firearm") "Reload"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Firearm") "TryReload"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Firearm") "TryUnload"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Firearm") "Unload"

Write-Host "=========== EXILED THROWABLE.Throw IL ===========" -ForegroundColor Green
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Throwable") "Throw"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Throwable") "CancelThrow"

Write-Host "=========== EXILED MICROHID.Fire / Recharge IL ===========" -ForegroundColor Green
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.MicroHid") "Fire"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.MicroHid") "Recharge"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.MicroHid") "Explode"
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.MicroHid") "set_State"

Write-Host "=========== EXILED JAILBIRD.Break IL ===========" -ForegroundColor Green
Dump-Method-IL (Get-Type $asmExApi "Exiled.API.Features.Items.Jailbird") "Break"

# Now look at base game stuff
Write-Host "=========== InventorySystem.Items.Firearms.Firearm ===========" -ForegroundColor Green
$fa = Get-Type $asmCS "InventorySystem.Items.Firearms.Firearm"
if ($fa) {
    Write-Host "BaseType: $($fa.BaseType)"
    foreach ($m in $fa.Methods | Sort-Object Name) {
        if ($m.Name -match "(Shoot|Fire|Trigger|Aim|Reload|Unload|Equip|OnUnequipped|OnEquipped|SendRpc|Server)") {
            $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
            $mods = ""
            if ($m.IsPublic) { $mods += "public " }
            if ($m.IsStatic) { $mods += "static " }
            Write-Host "  $mods$($m.ReturnType.Name) $($m.Name)($params)"
        }
    }
    Write-Host "Properties:"
    foreach ($p in $fa.Properties | Sort-Object Name) {
        Write-Host "  $($p.PropertyType.Name) $($p.Name)"
    }
}

Write-Host ""
Write-Host "=========== InventorySystem.Items.ThrowableProjectiles.ThrowableItem ===========" -ForegroundColor Green
$ti = Get-Type $asmCS "InventorySystem.Items.ThrowableProjectiles.ThrowableItem"
if ($ti) {
    Write-Host "BaseType: $($ti.BaseType)"
    foreach ($m in $ti.Methods | Sort-Object Name) {
        $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        $mods = ""
        if ($m.IsPublic) { $mods += "public " }
        if ($m.IsStatic) { $mods += "static " }
        Write-Host "  $mods$($m.ReturnType.Name) $($m.Name)($params)"
    }
    Write-Host "Fields:"
    foreach ($f in $ti.Fields | Sort-Object Name) {
        if ($f.IsPublic) {
            Write-Host "  $($f.FieldType.Name) $($f.Name)"
        }
    }
}

Write-Host ""
Write-Host "=========== InventorySystem.Items.MicroHID.MicroHIDItem ===========" -ForegroundColor Green
$mh = Get-Type $asmCS "InventorySystem.Items.MicroHID.MicroHIDItem"
if ($mh) {
    Write-Host "BaseType: $($mh.BaseType)"
    foreach ($m in $mh.Methods | Sort-Object Name) {
        $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        $mods = ""
        if ($m.IsPublic) { $mods += "public " }
        if ($m.IsStatic) { $mods += "static " }
        Write-Host "  $mods$($m.ReturnType.Name) $($m.Name)($params)"
    }
}

Write-Host ""
Write-Host "=========== InventorySystem.Items.Jailbird.JailbirdItem ===========" -ForegroundColor Green
$jb = Get-Type $asmCS "InventorySystem.Items.Jailbird.JailbirdItem"
if ($jb) {
    Write-Host "BaseType: $($jb.BaseType)"
    foreach ($m in $jb.Methods | Sort-Object Name) {
        $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        $mods = ""
        if ($m.IsPublic) { $mods += "public " }
        if ($m.IsStatic) { $mods += "static " }
        Write-Host "  $mods$($m.ReturnType.Name) $($m.Name)($params)"
    }
}
