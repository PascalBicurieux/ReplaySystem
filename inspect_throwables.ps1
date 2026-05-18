# Reflection inspection of Throwable / ThrowableItem types via Mono.Cecil
# (Cecil reads PE metadata directly, no runtime binding/netstandard issues.)
$ErrorActionPreference = 'Continue'

$cecilDll     = 'C:\Users\jadbe\source\repos\ReplaySystem\Tools\mono.cecil\lib\net40\Mono.Cecil.dll'
$exiledDll    = 'C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Exiled.API.dll'
$asmCsharpDll = 'C:\Users\jadbe\.nuget\packages\exmod.exiled\9.13.3\lib\net48\Assembly-CSharp-Publicized.dll'
$firstpassDll = 'C:\Users\jadbe\source\repos\ReplaySystem\Libs\Assembly-CSharp-firstpass.dll'

[void][System.Reflection.Assembly]::LoadFrom($cecilDll)

function Read-Asm($path) {
    if (-not (Test-Path $path)) { Write-Host "MISSING: $path" -ForegroundColor Red; return $null }
    try {
        return [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    } catch { Write-Host "Cecil failed on $path : $_" -ForegroundColor Red; return $null }
}

$asmExiled    = Read-Asm $exiledDll
$asmCsharp    = Read-Asm $asmCsharpDll
$asmFirstpass = Read-Asm $firstpassDll

function Find-Type($fullName) {
    foreach ($a in @($asmExiled, $asmCsharp, $asmFirstpass)) {
        if ($null -eq $a) { continue }
        foreach ($mod in $a.Modules) {
            $t = $mod.GetType($fullName)
            if ($t) { return $t }
        }
    }
    return $null
}

function Format-TypeRef($tr) {
    if ($null -eq $tr) { return '<null>' }
    return $tr.Name
}

function Format-Method($m) {
    $params = ($m.Parameters | ForEach-Object { "$(Format-TypeRef $_.ParameterType) $($_.Name)" }) -join ', '
    $mods = @()
    if ($m.IsStatic)   { $mods += 'static' }
    if ($m.IsVirtual)  { $mods += 'virtual' }
    $vis = if ($m.IsPublic) { 'public' } elseif ($m.IsFamily) { 'protected' } elseif ($m.IsAssembly) { 'internal' } else { 'private' }
    $modStr = if ($mods.Count) { ' ' + ($mods -join ' ') } else { '' }
    return "  [$vis]$modStr $(Format-TypeRef $m.ReturnType) $($m.Name)($params)"
}

function Dump-Type($t, [string]$header) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host $header -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    if ($null -eq $t) { Write-Host "  <type not found>" -ForegroundColor Red; return }
    Write-Host "FullName : $($t.FullName)"
    Write-Host "Module   : $($t.Module.Name)"
    Write-Host "BaseType : $($t.BaseType)"

    Write-Host "`n-- Public Properties (declared) --" -ForegroundColor Yellow
    $t.Properties | Where-Object {
        ($_.GetMethod -and $_.GetMethod.IsPublic) -or ($_.SetMethod -and $_.SetMethod.IsPublic)
    } | Sort-Object Name | ForEach-Object {
        $g = if ($_.GetMethod) { 'get;' } else { '' }
        $s = if ($_.SetMethod) { 'set;' } else { '' }
        Write-Host ("  $(Format-TypeRef $_.PropertyType) {0} {{ {1} {2} }}" -f $_.Name, $g, $s)
    }

    Write-Host "`n-- Public Fields (declared) --" -ForegroundColor Yellow
    $t.Fields | Where-Object { $_.IsPublic } | Sort-Object Name | ForEach-Object {
        $stat = if ($_.IsStatic) { 'static ' } else { '' }
        Write-Host ("  {0}$(Format-TypeRef $_.FieldType) {1}" -f $stat, $_.Name)
    }

    Write-Host "`n-- Public Methods (declared) --" -ForegroundColor Yellow
    $t.Methods | Where-Object { $_.IsPublic -and -not $_.IsGetter -and -not $_.IsSetter } |
        Sort-Object Name | ForEach-Object { Write-Host (Format-Method $_) }

    Write-Host "`n-- Nested Types --" -ForegroundColor Yellow
    $t.NestedTypes | ForEach-Object { Write-Host "  $($_.FullName)" }
}

# 1) Exiled Throwable + parent Item
$throwable = Find-Type 'Exiled.API.Features.Items.Throwable'
Dump-Type $throwable 'Exiled.API.Features.Items.Throwable'

if ($throwable -and $throwable.BaseType) {
    $parentFull = $throwable.BaseType.FullName
    $parent = Find-Type $parentFull
    Dump-Type $parent ("PARENT: " + $parentFull)
    if ($parent -and $parent.BaseType) {
        $grand = Find-Type $parent.BaseType.FullName
        Dump-Type $grand ("GRANDPARENT: " + $parent.BaseType.FullName)
    }
}

# 2) ThrowableItem (inventory side)
$throwableItem = Find-Type 'InventorySystem.Items.ThrowableProjectiles.ThrowableItem'
Dump-Type $throwableItem 'InventorySystem.Items.ThrowableProjectiles.ThrowableItem'

# 3) Related types
$ps = Find-Type 'InventorySystem.Items.ThrowableProjectiles.ProjectileSettings'
Dump-Type $ps 'InventorySystem.Items.ThrowableProjectiles.ProjectileSettings'

$tp = Find-Type 'InventorySystem.Items.ThrowableProjectiles.ThrownProjectile'
Dump-Type $tp 'InventorySystem.Items.ThrowableProjectiles.ThrownProjectile'

$tnh = Find-Type 'InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler'
Dump-Type $tnh 'InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler'

$eg = Find-Type 'InventorySystem.Items.ThrowableProjectiles.ExplosionGrenade'
Dump-Type $eg 'InventorySystem.Items.ThrowableProjectiles.ExplosionGrenade'

# 4) ALL Throw/Server/Cmd methods on ThrowableItem (any visibility)
Write-Host "`n=== ThrowableItem: ALL Throw/Server/Cmd/Rpc methods (any visibility) ===" -ForegroundColor Magenta
if ($throwableItem) {
    $throwableItem.Methods | Where-Object { $_.Name -match 'Throw|Server|Cmd|Rpc|Process' } |
        Sort-Object Name | ForEach-Object { Write-Host (Format-Method $_) }
}

# 5) Enumerate ALL types in ThrowableProjectiles namespace
Write-Host "`n=== All types in InventorySystem.Items.ThrowableProjectiles ===" -ForegroundColor Magenta
if ($asmCsharp) {
    foreach ($mod in $asmCsharp.Modules) {
        $mod.Types | Where-Object { $_.Namespace -eq 'InventorySystem.Items.ThrowableProjectiles' } |
            Sort-Object Name | ForEach-Object { Write-Host "  $($_.FullName)" }
    }
}
