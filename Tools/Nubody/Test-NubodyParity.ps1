param(
    [string] $ManifestPath = (Join-Path $PSScriptRoot "upstream-manifest.json"),
    [switch] $AllowDifferences,
    [switch] $ListPaths,
    [switch] $PathListOnly,
    [switch] $SummaryOnly
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

function Add-Path {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[string]] $Set,
        [AllowEmptyString()][string] $Path
    )

    $normalized = $Path.Trim().Replace('\', '/')
    if ($normalized.Length -gt 0) {
        [void] $Set.Add($normalized)
    }
}

$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json
$repoRootOutput = @(Invoke-Git -Arguments @("rev-parse", "--show-toplevel"))
$repoRoot = $repoRootOutput[0]
Set-Location $repoRoot

$source = $manifest.sourceCommit
Invoke-Git -Arguments @("cat-file", "-e", "$source^{commit}") | Out-Null
$engineTreeOutput = @(Invoke-Git -Arguments @("ls-tree", $source, "RobustToolbox"))
if ($engineTreeOutput.Count -ne 1 -or $engineTreeOutput[0] -notmatch '^160000 commit ([0-9a-f]{40})\s+RobustToolbox$') {
    throw "Unable to resolve the RobustToolbox gitlink at pinned source $source."
}

$sourceEngine = $Matches[1]
if ($sourceEngine -ne $manifest.engineCommit) {
    throw "Pinned source engine $sourceEngine does not match manifest engine $($manifest.engineCommit)."
}

$candidates = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($tree in $manifest.ownedTrees) {
    $prefix = $tree.TrimEnd('/').Replace('\', '/')
    foreach ($path in Invoke-Git -Arguments @("ls-tree", "-r", "--name-only", $source, "--", $prefix)) {
        Add-Path -Set $candidates -Path $path
    }

    foreach ($path in Invoke-Git -Arguments @("ls-files", "--", $prefix)) {
        Add-Path -Set $candidates -Path $path
    }
}

foreach ($commit in $manifest.selectionCommits) {
    Invoke-Git -Arguments @("cat-file", "-e", "$commit^{commit}") | Out-Null
    & git merge-base --is-ancestor $commit $source
    if ($LASTEXITCODE -ne 0) {
        throw "Selection commit $commit is not an ancestor of pinned source $source."
    }

    foreach ($path in Invoke-Git -Arguments @("show", "--pretty=format:", "--name-only", $commit)) {
        Add-Path -Set $candidates -Path $path
    }
}

foreach ($path in $manifest.ownedFiles) {
    Add-Path -Set $candidates -Path $path
}

foreach ($root in $manifest.removedRoots) {
    foreach ($path in Invoke-Git -Arguments @("ls-files", "--", $root)) {
        Add-Path -Set $candidates -Path $path
    }
}

$integrationGlue = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($path in $manifest.integrationGlue) {
    Add-Path -Set $integrationGlue -Path $path
}

$scope = @($candidates | Where-Object {
    $path = $_
    if ($integrationGlue.Contains($path)) {
        return $false
    }

    foreach ($prefix in $manifest.excludedPrefixes) {
        if ($path.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            return $false
        }
    }

    return $true
} | Sort-Object)

if ($PathListOnly) {
    $scope
    exit 0
}

if ($ListPaths) {
    $scope
}

$missing = [System.Collections.Generic.List[string]]::new()
$different = [System.Collections.Generic.List[string]]::new()
$unexpected = [System.Collections.Generic.List[string]]::new()
$matching = 0

foreach ($path in $scope) {
    & git cat-file -e "$source`:$path" 2>$null
    $sourceExists = $LASTEXITCODE -eq 0
    $localExists = Test-Path -LiteralPath $path -PathType Leaf

    if (-not $sourceExists) {
        if ($localExists) {
            $unexpected.Add($path)
        }
        continue
    }

    if (-not $localExists) {
        $missing.Add($path)
        continue
    }

    $sourceHashOutput = @(Invoke-Git -Arguments @("rev-parse", "$source`:$path"))
    $localHashOutput = @(Invoke-Git -Arguments @("hash-object", "--", $path))
    $sourceHash = $sourceHashOutput[0]
    $localHash = $localHashOutput[0]
    if ($sourceHash -eq $localHash) {
        $matching++
    } else {
        $different.Add($path)
    }
}

Write-Output "NuBody source: $source"
Write-Output "Owned scope: $($scope.Count) paths"
Write-Output "Matching: $matching"
Write-Output "Missing: $($missing.Count)"
Write-Output "Different: $($different.Count)"
Write-Output "Unexpected: $($unexpected.Count)"

if (-not $SummaryOnly) {
    foreach ($group in @(
        @{ Name = "MISSING"; Values = $missing },
        @{ Name = "DIFFERENT"; Values = $different },
        @{ Name = "UNEXPECTED"; Values = $unexpected }
    )) {
        if ($group.Values.Count -eq 0) {
            continue
        }

        Write-Output ""
        Write-Output "$($group.Name):"
        $group.Values | ForEach-Object { Write-Output "  $_" }
    }
}

$hasDifferences = $missing.Count -gt 0 -or $different.Count -gt 0 -or $unexpected.Count -gt 0
if ($hasDifferences -and -not $AllowDifferences) {
    exit 1
}

exit 0
