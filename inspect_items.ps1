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
$rp.ReadSymbols = $false

$asmExApi = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($exApi, $rp)
$asmExEvents = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($exEvents, $rp)
$asmCS = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmCSharp, $rp)

function Get-Type($asm, $fullName) {
    foreach ($t in $asm.MainModule.GetTypes()) {
        if ($t.FullName -eq $fullName) { return $t }
    }
    return $null
}

function Dump-Type($t, [string]$label) {
    if ($t -eq $null) { Write-Host "NOT FOUND: $label" -ForegroundColor Red; return }
    Write-Host ""
    Write-Host "===== $label =====" -ForegroundColor Cyan
    Write-Host "FullName: $($t.FullName)"
    Write-Host "BaseType: $($t.BaseType)"
    Write-Host "--- Methods ---" -ForegroundColor Yellow
    foreach ($m in $t.Methods | Sort-Object Name) {
        $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        $modifiers = ""
        if ($m.IsPublic) { $modifiers += "public " }
        elseif ($m.IsFamily) { $modifiers += "protected " }
        elseif ($m.IsAssembly) { $modifiers += "internal " }
        elseif ($m.IsPrivate) { $modifiers += "private " }
        if ($m.IsStatic) { $modifiers += "static " }
        if ($m.IsVirtual) { $modifiers += "virtual " }
        Write-Host "  $modifiers$($m.ReturnType.Name) $($m.Name)($params)"
    }
    Write-Host "--- Properties ---" -ForegroundColor Yellow
    foreach ($p in $t.Properties | Sort-Object Name) {
        $getSet = ""
        if ($p.GetMethod -ne $null) { $getSet += "get;" }
        if ($p.SetMethod -ne $null) { $getSet += "set;" }
        Write-Host "  $($p.PropertyType.Name) $($p.Name) { $getSet }"
    }
    Write-Host "--- Fields ---" -ForegroundColor Yellow
    foreach ($f in $t.Fields | Sort-Object Name) {
        if ($f.IsStatic -or $f.IsPublic) {
            $modifiers = ""
            if ($f.IsPublic) { $modifiers += "public " }
            if ($f.IsStatic) { $modifiers += "static " }
            Write-Host "  $modifiers$($f.FieldType.Name) $($f.Name)"
        }
    }
}

function Dump-Method-IL($t, [string]$methodName, [string]$label) {
    if ($t -eq $null) { return }
    Write-Host ""
    Write-Host "##### IL: $label.$methodName #####" -ForegroundColor Magenta
    foreach ($m in $t.Methods) {
        if ($m.Name -eq $methodName) {
            $params = ($m.Parameters | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ', '
            Write-Host "Signature: $($m.ReturnType.FullName) $($m.Name)($params)"
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

Write-Host "=================== EXILED API ITEMS ====================" -ForegroundColor Green
Dump-Type (Get-Type $asmExApi "Exiled.API.Features.Items.Firearm") "Exiled.API.Features.Items.Firearm"
Dump-Type (Get-Type $asmExApi "Exiled.API.Features.Items.Jailbird") "Exiled.API.Features.Items.Jailbird"
Dump-Type (Get-Type $asmExApi "Exiled.API.Features.Items.MicroHid") "Exiled.API.Features.Items.MicroHid"
Dump-Type (Get-Type $asmExApi "Exiled.API.Features.Items.Throwable") "Exiled.API.Features.Items.Throwable"
Dump-Type (Get-Type $asmExApi "Exiled.API.Features.Items.Item") "Exiled.API.Features.Items.Item"

Write-Host "=================== EXILED EVENTS ARGS ====================" -ForegroundColor Green
Dump-Type (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.ShotEventArgs") "ShotEventArgs"
Dump-Type (Get-Type $asmExEvents "Exiled.Events.EventArgs.Item.SwingingEventArgs") "SwingingEventArgs"
Dump-Type (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.ThrowingRequestEventArgs") "ThrowingRequestEventArgs"
Dump-Type (Get-Type $asmExEvents "Exiled.Events.EventArgs.Player.ThrownProjectileEventArgs") "ThrownProjectileEventArgs"

# Search for charging events
Write-Host "=================== CHARGING/MICROHID EVENTS ====================" -ForegroundColor Green
foreach ($t in $asmExEvents.MainModule.GetTypes()) {
    if ($t.FullName -match "(MicroHid|Microhid|MICROHID|Charging|Jailbird)") {
        Write-Host "Found event-related type: $($t.FullName)"
    }
}
