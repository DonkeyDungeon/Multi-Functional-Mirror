param(
    [Parameter(Mandatory)][string]$BuiltInEditor,
    [Parameter(Mandatory)][string]$SrpEditor,
    [Parameter(Mandatory)][ValidatePattern('^[0-9][0-9A-Za-z.-]*$')][string]$Version,
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$source = Split-Path -Parent $PSScriptRoot
$builtInEditorPath = (Resolve-Path -LiteralPath $BuiltInEditor).Path
$srpEditorPath = (Resolve-Path -LiteralPath $SrpEditor).Path
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$archiveNames = 'BuiltIn', 'URP', 'HDRP' | ForEach-Object { "Multi-Functional-Mirror_$($Version)_$_.unitypackage" }
foreach ($name in $archiveNames) {
    if (Test-Path -LiteralPath (Join-Path $destination $name)) {
        throw "Output package already exists: $name. Use an empty destination."
    }
}

$workspace = Join-Path ([IO.Path]::GetTempPath()) ('mfm-release-' + [guid]::NewGuid().ToString('N'))
$builtInProject = Join-Path $workspace 'BuiltIn'
$srpProject = Join-Path $workspace 'SRP'
$stagePackages = Join-Path $workspace 'PackagesOut'
$logs = Join-Path $destination 'ExportLogs'
$product = 'Assets/DonkeyDungeon/PlanarMirror'
$exporter = 'Assets/Authoring/Editor/StoreExport.cs'

function Copy-Asset([string]$relativePath, [string]$project) {
    $entry = Join-Path $source $relativePath
    $target = Join-Path $project $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $entry -Destination $target -Recurse
}

function Export-Pipeline([string]$editor, [string]$project, [string]$pipeline) {
    $log = Join-Path $logs "$pipeline.log"
    $arguments = @(
        '-batchmode', '-force-d3d11', '-job-worker-count', '2',
        '-projectPath', ('"' + $project + '"'),
        '-executeMethod', 'StoreExport.Export', '-mirrorPipeline', $pipeline,
        '-mirrorVersion', $Version,
        '-mirrorOutput', ('"' + $stagePackages + '"'),
        '-logFile', ('"' + $log + '"')
    )
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$pipeline export failed. See $log." }
    $package = Join-Path $stagePackages "Multi-Functional-Mirror_$($Version)_$pipeline.unitypackage"
    if (!(Test-Path -LiteralPath $package -PathType Leaf)) { throw "Missing $pipeline package. See $log." }
}

try {
    New-Item -ItemType Directory -Path $builtInProject, $srpProject, $stagePackages, $logs -Force | Out-Null
    $builtInAssets = @(
        'Assets/DonkeyDungeon.meta', "$product.meta",
        "$product/Documentation", "$product/Documentation.meta",
        "$product/Editor", "$product/Editor.meta",
        "$product/Textures", "$product/Textures.meta",
        "$product/Models", "$product/Models.meta",
        "$product/Runtime.meta", "$product/Runtime/PlanarMirror.cs", "$product/Runtime/PlanarMirror.cs.meta",
        "$product/Runtime/DonkeyDungeon.PlanarMirrors.asmdef", "$product/Runtime/DonkeyDungeon.PlanarMirrors.asmdef.meta",
        "$product/Shaders.meta", "$product/Shaders/PlanarMirrorBuiltIn.shader", "$product/Shaders/PlanarMirrorBuiltIn.shader.meta",
        "$product/Materials.meta", "$product/Materials/Builtin", "$product/Materials/Builtin.meta",
        "$product/Prefabs.meta", "$product/Prefabs/Builtin", "$product/Prefabs/Builtin.meta",
        "$product/Samples.meta", "$product/Samples/Builtin", "$product/Samples/Builtin.meta",
        $exporter, "$exporter.meta", 'LICENSE'
    )
    foreach ($asset in $builtInAssets) { Copy-Asset $asset $builtInProject }

    $manifestFolder = Join-Path $builtInProject 'Packages'
    $settingsFolder = Join-Path $builtInProject 'ProjectSettings'
    New-Item -ItemType Directory -Path $manifestFolder, $settingsFolder -Force | Out-Null
    '{"dependencies":{"com.unity.modules.imgui":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}}' |
        Set-Content -LiteralPath (Join-Path $manifestFolder 'manifest.json') -Encoding ascii
    'm_EditorVersion: 2022.3.51f1' |
        Set-Content -LiteralPath (Join-Path $settingsFolder 'ProjectVersion.txt') -Encoding ascii

    foreach ($entry in 'Assets', 'Packages', 'ProjectSettings', 'LICENSE') { Copy-Asset $entry $srpProject }
    Export-Pipeline $builtInEditorPath $builtInProject 'BuiltIn'
    Export-Pipeline $srpEditorPath $srpProject 'URP'
    Export-Pipeline $srpEditorPath $srpProject 'HDRP'

    foreach ($name in $archiveNames) {
        Move-Item -LiteralPath (Join-Path $stagePackages $name) -Destination (Join-Path $destination $name)
    }
    Write-Output "Exported three packages to $destination"
}
finally {
    if (Test-Path -LiteralPath $workspace) {
        $resolved = (Resolve-Path -LiteralPath $workspace).Path
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $resolved) -match '^mfm-release-[a-f0-9]{32}$') {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
