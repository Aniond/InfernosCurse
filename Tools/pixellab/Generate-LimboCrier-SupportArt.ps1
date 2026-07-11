param(
    [switch]$PortraitOnly,
    [switch]$IconsOnly,
    [switch]$StainOnly,
    [string]$PortraitJobId,
    [string]$StagingRoot = "GeneratedAssets/PixelLab/LimboCrier"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ApiRoot = "https://api.pixellab.ai/v2"
$Token = $env:PIXELLAB_SECRET
if ([string]::IsNullOrWhiteSpace($Token)) { $Token = $env:PIXELLAB_API_TOKEN }
if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "PIXELLAB_SECRET or PIXELLAB_API_TOKEN is required."
}
$Headers = @{ Authorization = "Bearer $Token" }
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$Stage = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $StagingRoot))
$SouthPath = Join-Path $Stage "rotations/south.png"
$SourcePath = Join-Path $Stage "source.json"
if (-not (Test-Path -LiteralPath $SouthPath)) { throw "Missing Crier south rotation at $SouthPath." }

function Base64-Image {
    param([string]$Path)
    return @{
        type = "base64"
        base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($Path))
        format = "png"
    }
}

function Base64-Image-Resized {
    param([string]$Path, [int]$Size)
    Add-Type -AssemblyName System.Drawing
    $sourceImage = [System.Drawing.Image]::FromFile($Path)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.DrawImage($sourceImage, 0, 0, $Size, $Size)
            }
            finally { $graphics.Dispose() }
            $stream = New-Object System.IO.MemoryStream
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                return @{
                    type = "base64"
                    base64 = [Convert]::ToBase64String($stream.ToArray())
                    format = "png"
                }
            }
            finally { $stream.Dispose() }
        }
        finally { $bitmap.Dispose() }
    }
    finally { $sourceImage.Dispose() }
}

function Invoke-PixelLab {
    param(
        [Parameter(Mandatory)][ValidateSet("GET", "POST")][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body
    )
    $uri = "$ApiRoot$Path"
    if ($Method -eq "GET") {
        return Invoke-RestMethod -Uri $uri -Method Get -Headers $Headers
    }
    return Invoke-RestMethod -Uri $uri -Method Post -Headers $Headers `
        -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 20 -Compress)
}

function Wait-Job {
    param([string]$JobId, [int]$TimeoutSeconds = 1800)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastStatus = ""
    while ([DateTime]::UtcNow -lt $deadline) {
        $job = Invoke-PixelLab -Method GET -Path "/background-jobs/$JobId"
        if ($job.status -ne $lastStatus) {
            Write-Host "PIXELLAB_JOB id=$($JobId.Substring(0,8)) status=$($job.status)"
            $lastStatus = $job.status
        }
        if ($job.status -eq "completed") { return $job }
        if ($job.status -eq "failed") {
            throw "PixelLab job failed: $($job.last_response | ConvertTo-Json -Depth 12 -Compress)"
        }
        Start-Sleep -Seconds 5
    }
    throw "PixelLab job $JobId timed out."
}

function Save-ApiImage {
    param([object]$Image, [string]$Path)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    if ($Image -is [string] -and $Image.StartsWith("http")) {
        Invoke-WebRequest -Uri $Image -OutFile $Path
        return
    }
    $value = if ($Image -is [string]) { $Image } else { [string]$Image.base64 }
    if ([string]::IsNullOrWhiteSpace($value)) { throw "PixelLab image payload was empty." }
    if ($value.StartsWith("data:")) { $value = $value.Substring($value.IndexOf(',') + 1) }
    [IO.File]::WriteAllBytes($Path, [Convert]::FromBase64String($value))
}

$generatePortrait = -not $IconsOnly -and -not $StainOnly
$generateIcons = -not $PortraitOnly -and -not $StainOnly
$generateStain = -not $PortraitOnly -and -not $IconsOnly
$style = Base64-Image $SouthPath

if ($generatePortrait) {
    if ([string]::IsNullOrWhiteSpace($PortraitJobId)) {
        Write-Output "PIXELLAB_PORTRAIT size=128 identity=Limbo_Crier"
        $response = Invoke-PixelLab -Method POST -Path "/portrait-character-pro" -Body @{
            direction = "character_to_portrait"
            image = $style
            view = "high top-down"
            result_size = 128
            seed = 12650721
        }
        $PortraitJobId = $response.background_job_id
        Write-Output "PIXELLAB_PORTRAIT_SUBMITTED job_id=$PortraitJobId"
    }
    $job = Wait-Job -JobId $PortraitJobId
    $images = @($job.last_response.images)
    if ($images.Count -lt 1) { throw "Portrait job returned no images." }
    Save-ApiImage -Image $images[0] -Path (Join-Path $Stage "support/portrait-limbo-crier.png")
    Write-Output "PIXELLAB_PORTRAIT_READY path=support/portrait-limbo-crier.png"
}

if ($generateIcons) {
    $icons = @(
        [ordered]@{ key="status-dread"; seed=12650722; prompt="Centered transparent 32x32 RPG status icon: one cracked black iron handbell emitting two restrained violet sound rings, ominous but readable, hard near-black one-pixel outline, three-tone pixel clusters, charcoal and dull iron with #6d3f68 violet, isolated single object." },
        [ordered]@{ key="status-false-zeal"; seed=12650723; prompt="Centered transparent 32x32 RPG status icon: one broken dull-gold halo visibly sewn together with dried-red and black thread, crooked asymmetrical circle, hard near-black one-pixel outline, three-tone pixel clusters, isolated single object." },
        [ordered]@{ key="item-bell-hook-staff"; seed=12650724; prompt="Centered transparent 32x32 RPG equipment icon: one iron-shod shepherd hook polearm shown diagonally, with a small cracked handbell hanging beneath the hook, dull iron and dark wood, hard near-black one-pixel outline, three-tone pixel clusters, isolated single object." },
        [ordered]@{ key="item-criers-jack"; seed=12650725; prompt="Centered transparent 32x32 RPG armor icon: one patched soot-brown quilted medieval gambeson, sleeveless torso view, dried-red repairs, worn cloth, hard near-black one-pixel outline, three-tone pixel clusters, isolated single object." },
        [ordered]@{ key="item-false-reliquary"; seed=12650726; prompt="Centered transparent 32x32 RPG accessory icon: one counterfeit old-parchment and dull-gold medieval reliquary pendant, tiny cracked window holding a dim restrained violet sliver, hard near-black one-pixel outline, three-tone pixel clusters, isolated single object." }
    )
    foreach ($icon in $icons) {
        Write-Output "PIXELLAB_ICON key=$($icon.key) size=32"
        $response = Invoke-PixelLab -Method POST -Path "/create-image-pixen" -Body @{
            description = "$($icon.prompt) No background, no scenery, no text, no letters, no numbers, no border, no UI frame, no hands, no blur, no gradients, no bloom, no photorealism, no 3D render."
            image_size = @{ width = 32; height = 32 }
            outline = "single color black outline"
            detail = "highly detailed"
            view = "low top-down"
            direction = "south-east"
            no_background = $true
            background_removal_task = "remove_complex_background"
            seed = $icon.seed
            enhance_prompt = $false
        }
        Save-ApiImage -Image $response.image -Path (Join-Path $Stage "support/$($icon.key).png")
        Write-Output "PIXELLAB_ICON_READY key=$($icon.key)"
    }
}

if ($generateStain) {
    Write-Output "PIXELLAB_DECAL key=limbo-stain size=128"
    $response = Invoke-PixelLab -Method POST -Path "/create-image-bitforge" -Body @{
        description = "Strict top-down transparent pixel-art tactical ground stain: sparse hairline charcoal and muted-violet cracks branching across one square tile, with only three disconnected curved sermon-stroke fragments suggesting an unfinished circle. Most of the canvas remains transparent. Asymmetrical, eroded, subtle, low intensity, hard crisp pixel clusters."
        negative_description = "complete circle, closed ring, pentagram, star, triangle, geometric seal, stone disc, coin, medallion, compass, readable runes, readable letters, alphabet, text, border, floor texture, object, character, fire, smoke, bloom, bright neon, gradient, blur, anti-aliasing"
        image_size = @{ width = 128; height = 128 }
        outline = "selective outline"
        shading = "flat shading"
        detail = "medium detail"
        view = "high top-down"
        direction = "south"
        no_background = $true
        coverage_percentage = 45.0
        text_guidance_scale = 12.0
        seed = 12650728
    }
    Save-ApiImage -Image $response.image -Path (Join-Path $Stage "support/limbo-stain.png")
    Write-Output "PIXELLAB_DECAL_READY key=limbo-stain"
}

if (Test-Path -LiteralPath $SourcePath) {
    $source = Get-Content -LiteralPath $SourcePath -Raw | ConvertFrom-Json
    $source | Add-Member -Force -NotePropertyName support_art -NotePropertyValue ([ordered]@{
        portrait_seed = 12650721
        icon_seeds = @{
            status_dread = 12650722
            status_false_zeal = 12650723
            item_bell_hook_staff = 12650724
            item_criers_jack = 12650725
            item_false_reliquary = 12650726
            limbo_stain = 12650728
        }
    })
    $source | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $SourcePath -Encoding utf8
}

Write-Output "PIXELLAB_SUPPORT_READY portrait=$generatePortrait icons=$generateIcons stain=$generateStain"
