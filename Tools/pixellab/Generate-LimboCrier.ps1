param(
    [switch]$NewCharacter,
    [switch]$GenerateAnimations,
    [string]$CharacterId,
    [string[]]$AnimationKeys = @(),
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
$SourcePath = Join-Path $Stage "source.json"
$DetailsPath = Join-Path $Stage "character-details.json"
[System.IO.Directory]::CreateDirectory($Stage) | Out-Null

$CharacterPrompt = @"
Common humanoid cultist and public doomsayer from late-medieval Florence, a mortal servant of Limbo. Full-body game character with a readable adult human silhouette. Soot-black hood and charcoal penitential robe over a patched brown quilted gambeson, dull iron half-mask hiding the upper face, narrow visible tired mouth, dried-blood red stitching and sash. Carries one distinctive iron-shod bell-hook staff: shepherd's hook polearm with a small cracked handbell hanging below the hook. Wears a counterfeit old-parchment reliquary at the chest containing a restrained dim violet sliver. Worn leather boots and gloves. Menacing but common low-level agitator, not an ornate priest, knight, wizard, or boss. Hard near-black pixel outline, crisp three-tone pixel clusters, muted palette of charcoal #29262b, soot brown #49372e, dull iron #697176, old parchment #c8b896, dried blood #7b3034, restrained violet #6d3f68, no gradients, no bloom. Keep the mask, robe, staff, cracked bell, and reliquary identical in every direction. Transparent background, no scenery, no text, no UI, no extra weapons, no modern details.
"@.Trim()

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
    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    return Invoke-RestMethod -Uri $uri -Method Post -Headers $Headers `
        -ContentType "application/json" -Body $json
}

function Wait-PixelLabJob {
    param(
        [Parameter(Mandatory)][string]$JobId,
        [int]$TimeoutSeconds = 1800
    )
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastStatus = ""
    while ([DateTime]::UtcNow -lt $deadline) {
        $job = Invoke-PixelLab -Method GET -Path "/background-jobs/$JobId"
        if ($job.status -ne $lastStatus) {
            Write-Output "PIXELLAB_JOB id=$($JobId.Substring(0,8)) status=$($job.status)"
            $lastStatus = $job.status
        }
        if ($job.status -eq "completed") { return $job }
        if ($job.status -eq "failed") {
            $detail = $job.last_response | ConvertTo-Json -Depth 12 -Compress
            throw "PixelLab job $JobId failed: $detail"
        }
        Start-Sleep -Seconds 5
    }
    throw "PixelLab job $JobId timed out after $TimeoutSeconds seconds."
}

function Save-Json {
    param([object]$Value, [string]$Path)
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-StyleReference {
    $path = Join-Path $ProjectRoot "Assets/Characters/Benidito/Sprites/rotations/south.png"
    Add-Type -AssemblyName System.Drawing
    $sourceImage = [System.Drawing.Image]::FromFile($path)
    try {
        $bitmap = New-Object System.Drawing.Bitmap 128, 128, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.DrawImage($sourceImage, 0, 0, 128, 128)
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

function Download-Image {
    param([string]$Url, [string]$Path)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    Invoke-WebRequest -Uri $Url -OutFile $Path
}

function Get-CharacterDetails {
    param([string]$CharacterId)
    $details = Invoke-PixelLab -Method GET -Path "/characters/$CharacterId"
    Save-Json $details $DetailsPath
    return $details
}

function Download-Rotations {
    param([object]$Details)
    $required = @("south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west")
    foreach ($direction in $required) {
        $property = $Details.rotation_urls.PSObject.Properties[$direction]
        if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw "Missing rotation URL for $direction."
        }
        Download-Image -Url ([string]$property.Value) `
            -Path (Join-Path $Stage "rotations/$direction.png")
    }
}

function Find-AnimationGroup {
    param([object]$Details, [string]$Key)
    foreach ($group in @($Details.animations)) {
        if ($group.display_name -eq $Key -or $group.animation_type -eq $Key) { return $group }
    }
    return $null
}

function Download-AnimationGroup {
    param([object]$Group, [string]$Key, [int]$ExpectedFrames)
    $required = @("south", "east", "north", "west")
    foreach ($direction in $required) {
        $entry = @($Group.directions | Where-Object { $_.direction -eq $direction })
        if ($entry.Count -ne 1) { throw "Animation $Key has no unique $direction sequence." }
        if ([int]$entry[0].frame_count -ne $ExpectedFrames) {
            throw "Animation $Key/$direction expected $ExpectedFrames frames, got $($entry[0].frame_count)."
        }
        $frames = @($entry[0].frames)
        if ($frames.Count -ne $ExpectedFrames) {
            throw "Animation $Key/$direction URL count is $($frames.Count), expected $ExpectedFrames."
        }
        for ($i = 0; $i -lt $frames.Count; $i++) {
            Download-Image -Url ([string]$frames[$i]) `
                -Path (Join-Path $Stage ("animations/{0}/{1}/frame_{2:D3}.png" -f $Key, $direction, $i))
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($CharacterId)) {
    $source = [ordered]@{
        provider = "PixelLab"
        character_id = $CharacterId
        creation_job_id = ""
        seed = 12650710
        view = "high top-down"
        requested_frame_size = @{ width = 128; height = 128 }
        prompt = $CharacterPrompt
        animations = @()
    }
    Save-Json $source $SourcePath
}
elseif ($NewCharacter -or -not (Test-Path -LiteralPath $SourcePath)) {
    Write-Output "PIXELLAB_CREATE character=Limbo_Crier mode=pro size=128 view=high_top_down"
    $response = Invoke-PixelLab -Method POST -Path "/create-character-pro" -Body @{
        description = $CharacterPrompt
        image_size = @{ width = 128; height = 128 }
        method = "create_with_style"
        view = "high top-down"
        template_id = "mannequin"
        no_background = $true
        reference_image = (Get-StyleReference)
        style_description = "Match the reference's crisp high-top-down HD-2D humanoid sprite scale, hard pixel clusters, outline weight, transparent padded canvas, and restrained three-tone shading. Do not copy its white-and-gold clothing."
        seed = 12650718
    }
    $source = [ordered]@{
        provider = "PixelLab"
        character_id = $response.character_id
        creation_job_id = $response.background_job_id
        seed = 12650718
        view = "high top-down"
        requested_frame_size = @{ width = 128; height = 128 }
        prompt = $CharacterPrompt
        animations = @()
    }
    Save-Json $source $SourcePath
    Write-Output "PIXELLAB_SUBMITTED character_id=$($response.character_id) job_id=$($response.background_job_id)"
    Wait-PixelLabJob -JobId $response.background_job_id | Out-Null
}

$source = Get-Content -LiteralPath $SourcePath -Raw | ConvertFrom-Json
$characterId = [string]$source.character_id
if ([string]::IsNullOrWhiteSpace($characterId)) { throw "Staging source.json has no character_id." }

$details = Get-CharacterDetails -CharacterId $characterId
Download-Rotations -Details $details
Write-Output "PIXELLAB_READY character_id=$characterId rotations=8"

if ($GenerateAnimations) {
    $directions = @("south", "east", "north", "west")
    $specs = @(
        [ordered]@{ key="walking"; frames=6; generated=6; keep_first=$false; action="A cautious slow walk while carrying the bell-hook staff upright; robe hems shift subtly; complete seamless locomotion cycle; feet remain grounded."; seed=12650711 },
        [ordered]@{ key="bell_hook_jab"; frames=9; generated=8; keep_first=$true; action="A short adjacent jab with the iron-shod bell-hook staff, one controlled thrust and return to the exact starting guard; do not swing overhead."; seed=12650712 },
        [ordered]@{ key="knell_of_dread"; frames=9; generated=8; keep_first=$true; action="Raise the cracked handbell on the hook, ring it once with a restrained violet pulse, then return to the exact starting stance; no large magic explosion."; seed=12650713 },
        [ordered]@{ key="crooked_benediction"; frames=9; generated=8; keep_first=$true; action="Point the false reliquary and crooked staff toward an ally in a blasphemous blessing, a brief crooked halo gesture, then return to the exact starting stance."; seed=12650714 },
        [ordered]@{ key="pilgrims_hook"; frames=9; generated=8; keep_first=$true; action="Extend the hooked staff two steps forward, catch and pull backward one strong beat, then return to the exact starting stance; keep both hands on the same staff."; seed=12650715 },
        [ordered]@{ key="hurt"; frames=4; generated=4; keep_first=$false; action="A restrained hit reaction: recoil from one impact, robe and bell jolt, regain balance; no attack and no falling."; seed=12650716 },
        [ordered]@{ key="death"; frames=9; generated=8; keep_first=$true; action="A complete defeated fall: knees fail, bell-hook staff slips with the body, collapse onto the ground and remain still in the final frame; no gore and no recovery."; seed=12650717 }
    )
    if ($AnimationKeys.Count -gt 0) {
        $specs = @($specs | Where-Object { $AnimationKeys -contains $_.key })
        if ($specs.Count -ne $AnimationKeys.Count) {
            throw "One or more requested AnimationKeys are unknown: $($AnimationKeys -join ', ')."
        }
    }

    $manifestAnimations = @()
    foreach ($animation in $specs) {
        $details = Get-CharacterDetails -CharacterId $characterId
        $group = Find-AnimationGroup -Details $details -Key $animation.key
        if ($null -eq $group) {
            Write-Output "PIXELLAB_ANIMATE key=$($animation.key) directions=4 expected_frames=$($animation.frames)"
            $response = Invoke-PixelLab -Method POST -Path "/characters/animations" -Body @{
                character_id = $characterId
                animation_name = $animation.key
                description = $CharacterPrompt
                action_description = $animation.action
                mode = "v3"
                frame_count = $animation.generated
                keep_first_frame = $animation.keep_first
                directions = $directions
                isometric = $false
                seed = $animation.seed
                enhance_prompt = $false
            }
            foreach ($jobId in @($response.background_job_ids)) {
                Wait-PixelLabJob -JobId ([string]$jobId) | Out-Null
            }
            $details = Get-CharacterDetails -CharacterId $characterId
            $group = Find-AnimationGroup -Details $details -Key $animation.key
        }
        if ($null -eq $group) { throw "Completed animation $($animation.key) was not returned by character details." }
        Download-AnimationGroup -Group $group -Key $animation.key -ExpectedFrames $animation.frames
        $manifestAnimations += [ordered]@{
            key = $animation.key
            animation_group_id = $group.animation_group_id
            expected_frames_per_direction = $animation.frames
            directions = $directions
            action_prompt = $animation.action
            seed = $animation.seed
        }
        Write-Output "PIXELLAB_ANIMATION_READY key=$($animation.key) group_id=$($group.animation_group_id) frames_per_direction=$($animation.frames)"
    }

    $source.animations = $manifestAnimations
    Save-Json $source $SourcePath
    Write-Output "PIXELLAB_PACKAGE_READY character_id=$characterId animation_groups=$($manifestAnimations.Count)"
}
