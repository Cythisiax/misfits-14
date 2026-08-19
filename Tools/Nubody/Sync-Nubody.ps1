param(
    [string] $ManifestPath = (Join-Path $PSScriptRoot "upstream-manifest.json"),
    [switch] $Apply
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(Mandatory)][string[]] $Arguments)

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }

    return @($output)
}

function Invoke-BatchedGit {
    param(
        [Parameter(Mandatory)][string[]] $Prefix,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Paths
    )

    $batchSize = 40
    for ($offset = 0; $offset -lt $Paths.Count; $offset += $batchSize) {
        $last = [Math]::Min($offset + $batchSize - 1, $Paths.Count - 1)
        $batch = $Paths[$offset..$last]
        Invoke-Git -Arguments @($Prefix + "--" + $batch) | Out-Null
    }
}

$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json
$rootOutput = @(Invoke-Git -Arguments @("rev-parse", "--show-toplevel"))
$repoRoot = [IO.Path]::GetFullPath($rootOutput[0])
Set-Location $repoRoot

$branchOutput = @(Invoke-Git -Arguments @("branch", "--show-current"))
if ($branchOutput[0] -ne "feature/nubody-migration") {
    throw "Refusing to sync outside feature/nubody-migration. Current branch: $($branchOutput[0])"
}

Invoke-Git -Arguments @("show-ref", "--verify", "--quiet", "refs/tags/archive/shitmed-pre-nubody") | Out-Null
Invoke-Git -Arguments @("cat-file", "-e", "$($manifest.sourceCommit)^{commit}") | Out-Null

$status = @(Invoke-Git -Arguments @("status", "--porcelain=v1"))
if ($status.Count -gt 0) {
    throw "Refusing to sync a dirty working tree. Commit or restore the current work unit first."
}

$checker = Join-Path $PSScriptRoot "Test-NubodyParity.ps1"
$scope = @(& pwsh -NoProfile -File $checker -ManifestPath $ManifestPath -PathListOnly)
if ($LASTEXITCODE -ne 0 -or $scope.Count -eq 0) {
    throw "Unable to generate the NuBody ownership scope."
}

$fromSource = [System.Collections.Generic.List[string]]::new()
$remove = [System.Collections.Generic.List[string]]::new()
$rootPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar

foreach ($path in $scope) {
    $resolved = [IO.Path]::GetFullPath((Join-Path $repoRoot $path))
    if (-not $resolved.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escapes the repository: $path"
    }

    & git cat-file -e "$($manifest.sourceCommit)`:$path" 2>$null
    if ($LASTEXITCODE -eq 0) {
        $fromSource.Add($path)
    } else {
        $remove.Add($path)
    }
}

Write-Output "NuBody source: $($manifest.sourceCommit)"
Write-Output "Import/update: $($fromSource.Count)"
Write-Output "Remove: $($remove.Count)"

if (-not $Apply) {
    Write-Output "Dry run only. Re-run with -Apply to stage the atomic cutover."
    exit 0
}

Invoke-BatchedGit -Prefix @("checkout", $manifest.sourceCommit) -Paths $fromSource.ToArray()
Invoke-BatchedGit -Prefix @("rm", "--ignore-unmatch") -Paths $remove.ToArray()

Write-Output "Atomic NuBody file synchronization staged. Run strict parity before reconciliation."
