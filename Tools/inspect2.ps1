Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll"
Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.Rocks.dll"

$asmCs = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Assembly-CSharp-Publicized.dll"
$libDir = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48"

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($libDir)
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $resolver

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmCs, $rp)

function Print-Method($m, $label) {
    Write-Host "===== $label =====" -ForegroundColor Cyan
    if ($m.HasBody) {
        foreach ($i in $m.Body.Instructions) {
            Write-Host ("  {0,-12} {1}" -f $i.OpCode.Name, $i.Operand)
        }
    } else {
        Write-Host "  (no body)"
    }
}

$ipb = $asm.MainModule.GetType("InventorySystem.Items.Pickups.ItemPickupBase")

foreach ($m in $ipb.Methods) {
    if ($m.Name -eq "set_Position" -or $m.Name -eq "set_Rotation" -or $m.Name -eq "get_Position" -or $m.Name -eq "get_Rotation" -or $m.Name -eq "set_NetworkInfo" -or $m.Name -eq "InfoReceivedHook" -or $m.Name -eq "SerializeSyncVars" -or $m.Name -eq "DeserializeSyncVars") {
        Print-Method $m "$($m.Name)"
    }
}

# Print PickupSyncInfo struct fully
Write-Host "`n===== PickupSyncInfo =====" -ForegroundColor Yellow
$psi = $asm.MainModule.GetType("InventorySystem.Items.Pickups.PickupSyncInfo")
if ($psi.IsValueType) { Write-Host "Kind: struct" } else { Write-Host "Kind: class" }
Write-Host "BaseType: $($psi.BaseType.FullName)"
Write-Host "Fields:"
foreach ($f in $psi.Fields) {
    Write-Host "  $($f.FieldType.Name) $($f.Name)"
}
Write-Host "Properties:"
foreach ($p in $psi.Properties) {
    Write-Host "  $($p.PropertyType.Name) $($p.Name)"
}

# Print all attributes of ItemPickupBase fields (SyncVar?)
Write-Host "`n===== ItemPickupBase Fields With Attributes =====" -ForegroundColor Yellow
foreach ($f in $ipb.Fields) {
    $attrs = @()
    foreach ($ca in $f.CustomAttributes) { $attrs += $ca.AttributeType.Name }
    Write-Host ("  {0,-40} {1,-30} [{2}]" -f $f.Name, $f.FieldType.Name, ($attrs -join ", "))
}

# PickupPhysicsModule
Write-Host "`n===== PickupPhysicsModule =====" -ForegroundColor Yellow
$ppm = $asm.MainModule.GetType("InventorySystem.Items.Pickups.PickupPhysicsModule")
if ($ppm) {
    Write-Host "BaseType: $($ppm.BaseType.FullName)"
    Write-Host "Methods (related):"
    foreach ($m in $ppm.Methods) {
        if ($m.Name -match "Position|Sync|Server|Update|Tick|Rpc|Network") {
            Write-Host "  $($m.FullName)"
        }
    }
    Write-Host "Derived types:"
    foreach ($t in $asm.MainModule.Types) {
        if ($t.BaseType -and $t.BaseType.FullName -eq "InventorySystem.Items.Pickups.PickupPhysicsModule") {
            Write-Host "  $($t.FullName)"
        }
    }
    # also look for nested
    Write-Host "Nested types of PickupPhysicsModule:"
    foreach ($n in $ppm.NestedTypes) {
        Write-Host "  $($n.FullName)"
    }
}

# Look for PickupStandardPhysics or similar
Write-Host "`n===== Subclasses of PickupPhysicsModule =====" -ForegroundColor Yellow
foreach ($t in $asm.MainModule.GetTypes()) {
    $bt = $t.BaseType
    while ($bt) {
        if ($bt.FullName -eq "InventorySystem.Items.Pickups.PickupPhysicsModule") {
            Write-Host "  $($t.FullName)"
            break
        }
        try { $bt = $bt.Resolve().BaseType } catch { break }
    }
}
