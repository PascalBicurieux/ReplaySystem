Add-Type -Path "C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll"
$asmCs = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Assembly-CSharp-Publicized.dll"
$libDir = "C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48"
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($libDir)
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $resolver
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmCs, $rp)

$ipb = $asm.MainModule.GetType("InventorySystem.Items.Pickups.ItemPickupBase")
foreach ($m in $ipb.Methods) {
    if ($m.Name -match "SendPhysicsModuleRpc|InvokeUserCode|UserCode_") {
        Write-Host "===== $($m.Name) ====="
        if ($m.HasBody) {
            foreach ($i in $m.Body.Instructions) {
                Write-Host ("  {0,-12} {1}" -f $i.OpCode.Name, $i.Operand)
            }
        }
    }
}

# Custom attributes on SendPhysicsModuleRpc
$mrpc = $ipb.Methods | Where-Object { $_.Name -eq "SendPhysicsModuleRpc" } | Select-Object -First 1
if ($mrpc) {
    Write-Host "Custom Attributes on SendPhysicsModuleRpc:"
    foreach ($ca in $mrpc.CustomAttributes) { Write-Host "  $($ca.AttributeType.FullName)" }
}
