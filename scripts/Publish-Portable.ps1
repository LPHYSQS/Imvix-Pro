param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$Restore,
    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\\publish\\portable")
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "..\\Imvix Pro.csproj"
$nugetConfigPath = Join-Path $PSScriptRoot "..\\NuGet.Config"
$requiredOcrAssets = @(
    "ch_PP-OCRv5_mobile_det.onnx",
    "ch_ppocr_mobile_v2.0_cls_infer.onnx",
    "ch_PP-OCRv5_rec_mobile_infer.onnx",
    "ppocrv5_dict.txt",
    "en_PP-OCRv5_rec_mobile_infer.onnx",
    "ppocrv5_en_dict.txt",
    "latin_PP-OCRv5_rec_mobile_infer.onnx",
    "ppocrv5_latin_dict.txt",
    "korean_PP-OCRv5_rec_mobile_infer.onnx",
    "ppocrv5_korean_dict.txt",
    "eslav_PP-OCRv5_rec_mobile_infer.onnx",
    "ppocrv5_eslav_dict.txt",
    "arabic_PP-OCRv5_rec_mobile_infer.onnx",
    "ppocrv5_arabic_dict.txt"
)
$requiredAiModels = @(
    "everyday-photo-4x",
    "anime-illustration-4x",
    "fast-lightweight-x4"
)

$runtimes = @($Runtime)

foreach ($rid in $runtimes) {
    $publishDir = Join-Path $OutputRoot $rid

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    $restoreArguments = @(
        "restore",
        $projectPath,
        "-r", $rid
    )

    if (Test-Path $nugetConfigPath) {
        $restoreArguments += @("--configfile", $nugetConfigPath)
    }

    if (-not $Restore) {
        $restoreArguments += "--ignore-failed-sources"
    }

    & dotnet @restoreArguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for $rid."
    }

    $publishArguments = @(
        "publish",
        $projectPath,
        "-c", $Configuration,
        "-r", $rid,
        "--self-contained", "true",
        "--no-restore",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:PublishReadyToRun=false",
        "-o", $publishDir
    )

    & dotnet @publishArguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid."
    }

    $nativeDir = Join-Path $publishDir ($(if ($rid -eq "win-x64") { "x64" } else { "x86" }))
    $runtimeDir = Join-Path $publishDir "runtime"
    $aiEngineDir = Join-Path $runtimeDir "ai\\enhancement\\engine"
    $aiModelsDir = Join-Path $runtimeDir "ai\\enhancement\\models"
    $aiInpaintingDir = Join-Path $runtimeDir "ai\\inpainting\\models\\LaMa"
    $aiMattingDir = Join-Path $runtimeDir "ai\\matting\\models"
    $requiredPaths = @(
        (Join-Path $publishDir "Imvix Pro.exe"),
        (Join-Path $publishDir "RapidOcrNet.dll"),
        (Join-Path $publishDir "zxing.dll"),
        (Join-Path $runtimeDir "qr\\zxing.dll"),
        (Join-Path $runtimeDir "qr\\configs\\decoder.json"),
        (Join-Path $runtimeDir "barcode\\zxing.dll"),
        (Join-Path $runtimeDir "barcode\\configs\\decoder.json"),
        (Join-Path $aiEngineDir "realesrgan-ncnn-vulkan.exe"),
        (Join-Path $aiEngineDir "vcomp140.dll"),
        (Join-Path $aiInpaintingDir "lama.onnx"),
        (Join-Path $aiMattingDir "MODNet\\model.onnx"),
        (Join-Path $aiMattingDir "U2Net\\model.onnx")
    )

    foreach ($file in $requiredOcrAssets) {
        $requiredPaths += Join-Path $runtimeDir "ocr\\paddle\\v5\\$file"
    }

    foreach ($model in $requiredAiModels) {
        $requiredPaths += Join-Path $aiModelsDir "$model.param"
        $requiredPaths += Join-Path $aiModelsDir "$model.bin"
    }

    $missingPaths = $requiredPaths | Where-Object { -not (Test-Path $_) }
    if ($missingPaths.Count -gt 0) {
        throw "Publish output for $rid is incomplete:`n$($missingPaths -join [Environment]::NewLine)"
    }

    Write-Host "Portable AI + OCR + QR + Barcode publish ready: $publishDir"
}
