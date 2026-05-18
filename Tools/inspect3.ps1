Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll"

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
    } else { Write-Host "  (no body)" }
}

# Inspect PickupStandardPhysics fully
Write-Host "===== PickupStandardPhysics =====" -ForegroundColor Yellow
$psp = $asm.MainModule.GetType("InventorySystem.Items.Pickups.PickupStandardPhysics")
Write-Host "BaseType: $($psp.BaseType.FullName)"
Write-Host "Fields:"
foreach ($f in $psp.Fields) {
    $attrs = @()
    foreach ($ca in $f.CustomAttributes) { $attrs += $ca.AttributeType.Name }
    Write-Host ("  {0,-40} {1,-30} [{2}]" -f $f.Name, $f.FieldType.Name, ($attrs -join ", "))
}
Write-Host "Properties:"
foreach ($p in $psp.Properties) {
    Write-Host "  $($p.PropertyType.Name) $($p.Name)"
}
Write-Host "Methods:"
foreach ($m in $psp.Methods) {
    Write-Host "  $($m.FullName)"
}

# Print bodies of key methods
foreach ($m in $psp.Methods) {
    if ($m.Name -match "Position|Sync|Rpc|Update|Server|Client|Tick|Write|Read|Send|Process") {
        Print-Method $m "PickupStandardPhysics.$($m.Name)"
    }
}

# Look at the base ServerSendRpc / ServerSetSyncData methods too
Write-Host "`n===== PickupPhysicsModule key methods =====" -ForegroundColor Yellow
$ppm = $asm.MainModule.GetType("InventorySystem.Items.Pickups.PickupPhysicsModule")
foreach ($m in $ppm.Methods) {
    Write-Host "  $($m.FullName)"
}
foreach ($m in $ppm.Methods) {
    if ($m.Name -match "ServerSendRpc|ServerSetSyncData|ClientProcessRpc|ClientReadSyncData|ctor") {
        Print-Method $m "PickupPhysicsModule.$($m.Name)"
    }
}

# Inspect PickupSyncInfo carefully (any nested or fields-only?)
Write-Host "`n===== PickupSyncInfo Methods/.ctor =====" -ForegroundColor Yellow
$psi = $asm.MainModule.GetType("InventorySystem.Items.Pickups.PickupSyncInfo")
foreach ($m in $psi.Methods) {
    Write-Host "  $($m.FullName)"
}
