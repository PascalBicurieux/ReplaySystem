Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll"
Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.Rocks.dll"

$exiledDll = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Exiled.API.dll"
$asmCs = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Assembly-CSharp-Publicized.dll"
$libDir = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48"

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($libDir)
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $resolver

$exiled = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($exiledDll, $rp)
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmCs, $rp)

function Print-Method($m, $label) {
    Write-Host "===== $label =====" -ForegroundColor Cyan
    Write-Host "Signature: $($m.FullName)"
    Write-Host "Has body: $($m.HasBody)"
    if ($m.HasBody) {
        foreach ($i in $m.Body.Instructions) {
            Write-Host ("  {0,-12} {1}" -f $i.OpCode.Name, $i.Operand)
        }
    }
}

# Exiled Pickup
$pickupType = $exiled.MainModule.GetType("Exiled.API.Features.Pickups.Pickup")
Write-Host "===== Exiled.API.Features.Pickups.Pickup =====" -ForegroundColor Yellow
foreach ($p in $pickupType.Properties) {
    if ($p.Name -eq "Position" -or $p.Name -eq "Rotation") {
        Write-Host "Property: $($p.Name) -- Type: $($p.PropertyType.FullName)"
        if ($p.SetMethod) { Print-Method $p.SetMethod "Setter $($p.Name)" }
        if ($p.GetMethod) {
            Write-Host "--- Getter $($p.Name) ---"
            foreach ($i in $p.GetMethod.Body.Instructions) {
                Write-Host ("  {0,-12} {1}" -f $i.OpCode.Name, $i.Operand)
            }
        }
    }
}

# ItemPickupBase
$ipbType = $asm.MainModule.GetType("InventorySystem.Items.Pickups.ItemPickupBase")
Write-Host "`n===== InventorySystem.Items.Pickups.ItemPickupBase =====" -ForegroundColor Yellow
Write-Host "BaseType: $($ipbType.BaseType.FullName)"
Write-Host "Interfaces:"
foreach ($it in $ipbType.Interfaces) { Write-Host "  $($it.InterfaceType.FullName)" }
Write-Host "Fields:"
foreach ($f in $ipbType.Fields) {
    Write-Host "  [$($f.Attributes)] $($f.FieldType.Name) $($f.Name)"
}
Write-Host "Properties:"
foreach ($p in $ipbType.Properties) {
    Write-Host "  $($p.PropertyType.Name) $($p.Name)"
}
Write-Host "Methods (related to Position/Sync/Network):"
foreach ($m in $ipbType.Methods) {
    if ($m.Name -match "Position|Rotation|Sync|Network|Refresh|Info") {
        Write-Host "  $($m.FullName)"
    }
}
