param(
    [switch] $SummaryOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = (& git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to locate the repository root."
}

Set-Location $repoRoot

$ownedRoots = @(
    "Content.Client/_Shitmed",
    "Content.Server/_Shitmed",
    "Content.Shared/_Shitmed",
    "Resources/Prototypes/_Shitmed",
    "Resources/Locale/en-US/_Shitmed",
    "Resources/Textures/_Shitmed",
    "Resources/Audio/_Shitmed"
)

$owned = [ordered]@{}
foreach ($root in $ownedRoots) {
    $files = @(& git ls-files -- $root)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inventory $root."
    }

    $owned[$root] = $files
}

$referenceArgs = @(
    "--line-number",
    "--no-heading",
    "--color", "never",
    "--ignore-case",
    "--glob", "!Resources/Maps/**",
    "--glob", "!Content.Client/_Shitmed/**",
    "--glob", "!Content.Server/_Shitmed/**",
    "--glob", "!Content.Shared/_Shitmed/**",
    "--glob", "!Resources/Prototypes/_Shitmed/**",
    "--glob", "!Resources/Locale/en-US/_Shitmed/**",
    "--glob", "!Resources/Textures/_Shitmed/**",
    "--glob", "!Resources/Audio/_Shitmed/**",
    "shitmed",
    "Content.Client", "Content.Server", "Content.Shared", "Resources"
)

$references = @(& rg @referenceArgs)
if ($LASTEXITCODE -notin @(0, 1)) {
    throw "rg failed with exit code $LASTEXITCODE."
}

$result = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    ownedCounts = [ordered]@{}
    externalReferenceCount = $references.Count
}

foreach ($entry in $owned.GetEnumerator()) {
    $result.ownedCounts[$entry.Key] = $entry.Value.Count
}

if (-not $SummaryOnly) {
    $result.ownedFiles = $owned
    $result.externalReferences = $references
}

$result | ConvertTo-Json -Depth 6
