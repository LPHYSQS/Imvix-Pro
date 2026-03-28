using namespace System.Collections.Generic
using namespace System.IO
using namespace System.Text

$translations = [ordered]@{
    'en-US' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI Enhancement'
            AiEnhancementToggleHint = 'Enable AI enhancement preprocessing before the original conversion flow.'
            AiEnhancementTab = 'AI Enhancement'
            AiEnhancementPanelTitle = 'AI Enhancement'
            AiEnhancementDescription = 'When enabled, supported images are enhanced first and then passed to the existing conversion pipeline.'
            AiScale = 'Upscale Ratio'
            AiModel = 'Model'
            AiExecutionMode = 'GPU Mode'
            AiExecutionHint = 'Auto tries Vulkan first and falls back to CPU mode when the bundled runtime supports it.'
            AiInputSupportHint = 'Static PNG, JPG, JPEG, WEBP, BMP, TIFF, and single-frame GIF images can be enhanced. PDFs, PSDs, SVGs, animated GIFs, and other sources continue through the original flow.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'Series: {0}'
            AiModelSelectedRestriction_NonCommercial = 'Warning: Non-commercial use only'
            AiModel_General = 'General'
            AiModel_Anime = 'Anime'
            AiModel_Lightweight = 'Lightweight'
            AiModel_UpscaylStandard = 'Standard (Recommended)'
            AiModel_UpscaylLite = 'Lite'
            AiModel_UpscaylHighFidelity = 'High Fidelity'
            AiModel_UpscaylDigitalArt = 'Digital Art'
            AiModel_UpscaylRemacri = 'Remacri (Non-commercial)'
            AiModel_UpscaylUltramix = 'Ultramix (Non-commercial)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp (Non-commercial)'
            AiModelDescription_General = 'Best for most images. Stable all-around results and the default choice.'
            AiModelDescription_Anime = 'Optimized for anime, illustrations, and line art.'
            AiModelDescription_Lightweight = 'Faster and lighter on hardware, with a modest quality trade-off.'
            AiModelDescription_UpscaylStandard = 'Best for most images, with balanced overall quality (recommended).'
            AiModelDescription_UpscaylLite = 'Best for most images, with faster processing and only a small quality trade-off.'
            AiModelDescription_UpscaylHighFidelity = 'Works well across many image types and prioritizes detail retention with smoother textures.'
            AiModelDescription_UpscaylDigitalArt = 'Designed for digital paintings, illustrations, and other art-focused images.'
            AiModelDescription_UpscaylRemacri = 'Best for natural images, with sharper details and stronger detail recovery.'
            AiModelDescription_UpscaylUltramix = 'Best for natural images, balancing sharpness and fine detail.'
            AiModelDescription_UpscaylUltrasharp = 'Best for natural images and tuned to emphasize stronger sharpness.'
            AiExecutionMode_Auto = 'Auto'
            AiExecutionMode_ForceCpu = 'Force CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'AI enhancement in progress. Please wait...'
            ConversionFeedbackTitle = 'AI conversion is in progress. Please wait patiently.'
            ConversionFeedbackDescription = 'Imvix Pro is still processing your files, and this does not mean the task has failed.'
            ConversionFeedbackHardwareHint = 'Processing speed depends on your computer hardware. Faster hardware usually finishes sooner, while lower-end hardware may need more time.'
            ConversionFeedbackCloseHint = 'Please keep Imvix Pro open during conversion and do not close the app.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} selected items are not eligible for AI enhancement and will continue through the original conversion flow.'
            AiModelFallbackToDefaultTemplate = 'The selected AI model "{0}" is unavailable, so "{1}" will be used automatically.'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = 'The selected file type cannot be prepared for AI enhancement.'
            AiErrorPrepareTemporaryImage = 'Unable to prepare a temporary image for AI enhancement.'
            AiErrorRuntimeFolderMissing = 'The bundled AI runtime folder is missing. Restore the AI directory and try again.'
            AiErrorRuntimeExecutableMissing = 'The bundled AI runtime executable is missing. Restore realesrgan-ncnn-vulkan.exe and try again.'
            AiErrorModelMissingTemplate = 'The required AI model ''{0}'' is missing.'
            AiErrorLightweightModelMissing = 'The required lightweight AI model files are missing.'
            AiErrorCpuModeUnsupported = 'The bundled AI runtime does not support CPU mode. Switch GPU mode to Auto or update the AI runtime package.'
            AiErrorModelLoadFailed = 'The required AI model files could not be loaded.'
            AiErrorProcessExitCodeTemplate = 'AI enhancement process failed with exit code {0}.'
            AiErrorGpuAttemptFailedTemplate = 'GPU attempt failed: {0}'
            AiErrorCpuFallbackFailedTemplate = 'CPU fallback failed: {0}'
            AiErrorPostResizeFailedTemplate = 'Failed to resize the AI output to the target scale: {0}'
        }
    }
    'zh-CN' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI增强'
            AiEnhancementToggleHint = '开启后，会在原有转换流程前先执行 AI 增强预处理。'
            AiEnhancementTab = 'AI增强'
            AiEnhancementPanelTitle = 'AI增强'
            AiEnhancementDescription = '启用后，受支持的图片会先进行 AI 增强，再交给现有转换流程处理。'
            AiScale = '放大倍率'
            AiModel = '模型选择'
            AiExecutionMode = 'GPU模式'
            AiExecutionHint = '自动模式会优先尝试 Vulkan；如果当前捆绑运行时支持，再回退到 CPU 模式。'
            AiInputSupportHint = '支持静态 PNG、JPG、JPEG、WEBP、BMP、TIFF 和单帧 GIF 图片增强。PDF、PSD、SVG、动态图 GIF 等文件会继续走原有流程。'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = '系列：{0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 仅限非商业用途'
            AiModel_General = '通用'
            AiModel_Anime = '动漫'
            AiModel_Lightweight = '轻量'
            AiModel_UpscaylStandard = '标准（推荐）'
            AiModel_UpscaylLite = '轻量'
            AiModel_UpscaylHighFidelity = '高保真'
            AiModel_UpscaylDigitalArt = '数字艺术'
            AiModel_UpscaylRemacri = 'Remacri（非商业）'
            AiModel_UpscaylUltramix = 'Ultramix（非商业）'
            AiModel_UpscaylUltrasharp = 'Ultrasharp（非商业）'
            AiModelDescription_General = '适用于大多数图像，综合效果稳定均衡，也是默认选择。'
            AiModelDescription_Anime = '适用于动漫、插画和线稿等图像。'
            AiModelDescription_Lightweight = '更注重速度与资源占用，画质损失相对较小。'
            AiModelDescription_UpscaylStandard = '适用于大多数图像，综合效果均衡（推荐）'
            AiModelDescription_UpscaylLite = '适用于大多数图像，处理速度更快，质量损失较小'
            AiModelDescription_UpscaylHighFidelity = '适用于各种图像，注重细节表现与纹理平滑'
            AiModelDescription_UpscaylDigitalArt = '适用于数字绘画、插图等艺术类图像'
            AiModelDescription_UpscaylRemacri = '适用于自然图像，增强锐度与细节'
            AiModelDescription_UpscaylUltramix = '适用于自然图像，在锐度与细节之间取得平衡'
            AiModelDescription_UpscaylUltrasharp = '适用于自然图像，强调锐度增强'
            AiExecutionMode_Auto = '自动'
            AiExecutionMode_ForceCpu = '强制CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'AI增强进行中，请耐心等待...'
            ConversionFeedbackTitle = 'AI 转换正在进行，请耐心等待'
            ConversionFeedbackDescription = 'Imvix Pro 仍在持续处理当前任务，这并不代表转换失败。'
            ConversionFeedbackHardwareHint = '转换速度与电脑硬件配置有关，配置越高通常越快，配置较低时耗时会相对更长。'
            ConversionFeedbackCloseHint = '转换过程中请保持 Imvix Pro 处于打开状态，不要关闭软件。'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} 个已选项目不适合 AI 增强，将继续走原有转换流程。'
            AiModelFallbackToDefaultTemplate = '当前所选 AI 模型“{0}”不可用，已自动回退为“{1}”。'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = '所选文件类型无法为 AI 增强进行预处理。'
            AiErrorPrepareTemporaryImage = '无法为 AI 增强准备临时图片。'
            AiErrorRuntimeFolderMissing = '缺少捆绑的 AI 运行时目录，请恢复 AI 目录后重试。'
            AiErrorRuntimeExecutableMissing = '缺少捆绑的 AI 可执行文件，请恢复 realesrgan-ncnn-vulkan.exe 后重试。'
            AiErrorModelMissingTemplate = '缺少所需的 AI 模型“{0}”。'
            AiErrorLightweightModelMissing = '缺少所需的轻量 AI 模型文件。'
            AiErrorCpuModeUnsupported = '当前捆绑的 AI 运行时不支持 CPU 模式。请将 GPU 模式切换为“自动”，或更新 AI 运行时包。'
            AiErrorModelLoadFailed = '无法加载所需的 AI 模型文件。'
            AiErrorProcessExitCodeTemplate = 'AI 增强进程失败，退出代码：{0}。'
            AiErrorGpuAttemptFailedTemplate = 'GPU 尝试失败：{0}'
            AiErrorCpuFallbackFailedTemplate = 'CPU 回退失败：{0}'
            AiErrorPostResizeFailedTemplate = '无法将 AI 输出缩放到目标倍率：{0}'
        }
    }
    'zh-TW' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI增強'
            AiEnhancementToggleHint = '開啟後，會在原有轉換流程前先執行 AI 增強前處理。'
            AiEnhancementTab = 'AI增強'
            AiEnhancementPanelTitle = 'AI增強'
            AiEnhancementDescription = '啟用後，受支援的圖片會先進行 AI 增強，再交給現有轉換流程處理。'
            AiScale = '放大倍率'
            AiModel = '模型選擇'
            AiExecutionMode = 'GPU模式'
            AiExecutionHint = '自動模式會優先嘗試 Vulkan；如果目前捆綁執行環境支援，再回退到 CPU 模式。'
            AiInputSupportHint = '支援靜態 PNG、JPG、JPEG、WEBP、BMP、TIFF 與單幀 GIF 圖片增強。PDF、PSD、SVG、動態 GIF 等檔案會繼續走原有流程。'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = '系列：{0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 僅限非商業用途'
            AiModel_General = '通用'
            AiModel_Anime = '動漫'
            AiModel_Lightweight = '輕量'
            AiModel_UpscaylStandard = '標準（推薦）'
            AiModel_UpscaylLite = '輕量'
            AiModel_UpscaylHighFidelity = '高保真'
            AiModel_UpscaylDigitalArt = '數位藝術'
            AiModel_UpscaylRemacri = 'Remacri（非商業）'
            AiModel_UpscaylUltramix = 'Ultramix（非商業）'
            AiModel_UpscaylUltrasharp = 'Ultrasharp（非商業）'
            AiModelDescription_General = '適用於大多數圖像，綜合效果穩定均衡，也是預設選擇。'
            AiModelDescription_Anime = '適用於動漫、插畫與線稿等圖像。'
            AiModelDescription_Lightweight = '更重視速度與資源占用，畫質損失相對較小。'
            AiModelDescription_UpscaylStandard = '適用於大多數圖像，綜合效果均衡（推薦）'
            AiModelDescription_UpscaylLite = '適用於大多數圖像，處理速度更快，品質損失較小'
            AiModelDescription_UpscaylHighFidelity = '適用於各類圖像，著重細節表現與紋理平滑'
            AiModelDescription_UpscaylDigitalArt = '適用於數位繪畫、插圖等藝術類圖像'
            AiModelDescription_UpscaylRemacri = '適用於自然圖像，增強銳利度與細節'
            AiModelDescription_UpscaylUltramix = '適用於自然圖像，在銳利度與細節之間取得平衡'
            AiModelDescription_UpscaylUltrasharp = '適用於自然圖像，強調銳利度增強'
            AiExecutionMode_Auto = '自動'
            AiExecutionMode_ForceCpu = '強制CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'AI增強進行中，請耐心等待...'
            ConversionFeedbackTitle = 'AI 轉換正在進行，請耐心等待'
            ConversionFeedbackDescription = 'Imvix Pro 仍在持續處理目前任務，這不代表轉換失敗。'
            ConversionFeedbackHardwareHint = '轉換速度與電腦硬體配置有關，配置越高通常越快，配置較低時耗時會相對更長。'
            ConversionFeedbackCloseHint = '轉換過程中請保持 Imvix Pro 處於開啟狀態，不要關閉軟體。'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} 個已選項目不適合 AI 增強，將繼續走原有轉換流程。'
            AiModelFallbackToDefaultTemplate = '目前所選 AI 模型「{0}」不可用，已自動回退為「{1}」。'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = '所選檔案類型無法為 AI 增強進行前處理。'
            AiErrorPrepareTemporaryImage = '無法為 AI 增強準備暫存圖片。'
            AiErrorRuntimeFolderMissing = '缺少捆綁的 AI 執行環境目錄，請還原 AI 目錄後再試。'
            AiErrorRuntimeExecutableMissing = '缺少捆綁的 AI 可執行檔，請還原 realesrgan-ncnn-vulkan.exe 後再試。'
            AiErrorModelMissingTemplate = '缺少所需的 AI 模型「{0}」。'
            AiErrorLightweightModelMissing = '缺少所需的輕量 AI 模型檔案。'
            AiErrorCpuModeUnsupported = '目前捆綁的 AI 執行環境不支援 CPU 模式。請將 GPU 模式切換為「自動」，或更新 AI 執行環境套件。'
            AiErrorModelLoadFailed = '無法載入所需的 AI 模型檔案。'
            AiErrorProcessExitCodeTemplate = 'AI 增強程序失敗，結束代碼：{0}。'
            AiErrorGpuAttemptFailedTemplate = 'GPU 嘗試失敗：{0}'
            AiErrorCpuFallbackFailedTemplate = 'CPU 回退失敗：{0}'
            AiErrorPostResizeFailedTemplate = '無法將 AI 輸出縮放到目標倍率：{0}'
        }
    }
    'ja-JP' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI高画質化'
            AiEnhancementToggleHint = '有効にすると、既存の変換フローの前に AI 強化の前処理を実行します。'
            AiEnhancementTab = 'AI高画質化'
            AiEnhancementPanelTitle = 'AI高画質化'
            AiEnhancementDescription = '有効時は、対応画像を先に AI で高画質化してから既存の変換パイプラインへ渡します。'
            AiScale = '拡大倍率'
            AiModel = 'モデル'
            AiExecutionMode = 'GPUモード'
            AiExecutionHint = '自動はまず Vulkan を試し、同梱ランタイムが対応していれば CPU モードにフォールバックします。'
            AiInputSupportHint = '静止画の PNG、JPG、JPEG、WEBP、BMP、TIFF、および単一フレーム GIF を高画質化できます。PDF、PSD、SVG、アニメーション GIF などは従来フローのまま処理されます。'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'シリーズ: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 非商用利用に限ります'
            AiModel_General = '汎用'
            AiModel_Anime = 'アニメ'
            AiModel_Lightweight = '軽量'
            AiModel_UpscaylStandard = '標準（推奨）'
            AiModel_UpscaylLite = '軽量'
            AiModel_UpscaylHighFidelity = '高忠実度'
            AiModel_UpscaylDigitalArt = 'デジタルアート'
            AiModel_UpscaylRemacri = 'Remacri（非商用）'
            AiModel_UpscaylUltramix = 'Ultramix（非商用）'
            AiModel_UpscaylUltrasharp = 'Ultrasharp（非商用）'
            AiModelDescription_General = '多くの画像に向く、安定した標準モデルで、既定の選択肢です。'
            AiModelDescription_Anime = 'アニメ、イラスト、線画に適しています。'
            AiModelDescription_Lightweight = 'より軽量で高速に処理でき、画質低下も比較的小さめです。'
            AiModelDescription_UpscaylStandard = '多くの画像に適した、全体のバランスが良いモデルです（推奨）'
            AiModelDescription_UpscaylLite = '多くの画像に適し、より高速で、画質低下も小さめです'
            AiModelDescription_UpscaylHighFidelity = '幅広い画像に対応し、細部表現と質感の滑らかさを重視します'
            AiModelDescription_UpscaylDigitalArt = 'デジタルペイント、イラストなどのアート系画像に適しています'
            AiModelDescription_UpscaylRemacri = '自然画像に適し、シャープさと細部の再現を強化します'
            AiModelDescription_UpscaylUltramix = '自然画像に適し、シャープさと細部のバランスを取ります'
            AiModelDescription_UpscaylUltrasharp = '自然画像に適し、より強いシャープさを重視します'
            AiExecutionMode_Auto = '自動'
            AiExecutionMode_ForceCpu = 'CPUを強制'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'AI高画質化を実行中です。しばらくお待ちください...'
            ConversionFeedbackTitle = 'AI 変換を実行中です。しばらくお待ちください'
            ConversionFeedbackDescription = 'Imvix Pro は現在も処理を続けており、失敗したわけではありません。'
            ConversionFeedbackHardwareHint = '処理速度はお使いの PC のハードウェア性能に左右されます。高性能な環境ほど速く、性能が低い環境では時間が長くかかることがあります。'
            ConversionFeedbackCloseHint = '変換中は Imvix Pro を開いたままにし、アプリを閉じないでください。'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} 件の選択項目は AI 高画質化の対象外のため、従来の変換フローで処理されます。'
            AiModelFallbackToDefaultTemplate = '選択した AI モデル「{0}」は利用できないため、「{1}」に自動で切り替えます。'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = '選択したファイル形式は AI 高画質化用に前処理できません。'
            AiErrorPrepareTemporaryImage = 'AI 高画質化用の一時画像を準備できませんでした。'
            AiErrorRuntimeFolderMissing = '同梱の AI ランタイム フォルダーが見つかりません。AI ディレクトリを復元してから再試行してください。'
            AiErrorRuntimeExecutableMissing = '同梱の AI 実行ファイルが見つかりません。realesrgan-ncnn-vulkan.exe を復元してから再試行してください。'
            AiErrorModelMissingTemplate = '必要な AI モデル ''{0}'' が見つかりません。'
            AiErrorLightweightModelMissing = '必要な軽量 AI モデル ファイルが見つかりません。'
            AiErrorCpuModeUnsupported = '同梱の AI ランタイムは CPU モードをサポートしていません。GPU モードを「自動」に切り替えるか、AI ランタイム パッケージを更新してください。'
            AiErrorModelLoadFailed = '必要な AI モデル ファイルを読み込めませんでした。'
            AiErrorProcessExitCodeTemplate = 'AI 高画質化プロセスが終了コード {0} で失敗しました。'
            AiErrorGpuAttemptFailedTemplate = 'GPU の試行に失敗しました: {0}'
            AiErrorCpuFallbackFailedTemplate = 'CPU フォールバックも失敗しました: {0}'
            AiErrorPostResizeFailedTemplate = 'AI 出力を目標倍率にリサイズできませんでした: {0}'
        }
    }
    'ko-KR' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI 향상'
            AiEnhancementToggleHint = '사용하면 기존 변환 흐름 전에 AI 향상 전처리를 실행합니다.'
            AiEnhancementTab = 'AI 향상'
            AiEnhancementPanelTitle = 'AI 향상'
            AiEnhancementDescription = '활성화하면 지원되는 이미지를 먼저 AI로 향상한 뒤 기존 변환 파이프라인으로 전달합니다.'
            AiScale = '확대 배율'
            AiModel = '모델'
            AiExecutionMode = 'GPU 모드'
            AiExecutionHint = '자동 모드는 먼저 Vulkan을 시도하고, 번들 런타임이 지원하면 CPU 모드로 전환합니다.'
            AiInputSupportHint = '정지 이미지 PNG, JPG, JPEG, WEBP, BMP, TIFF 및 단일 프레임 GIF를 향상할 수 있습니다. PDF, PSD, SVG, 애니메이션 GIF 등은 기존 흐름으로 계속 처리됩니다.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = '시리즈: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 비상업적 용도로만 사용 가능'
            AiModel_General = '일반'
            AiModel_Anime = '애니메이션'
            AiModel_Lightweight = '경량'
            AiModel_UpscaylStandard = '표준(권장)'
            AiModel_UpscaylLite = '라이트'
            AiModel_UpscaylHighFidelity = '고충실도'
            AiModel_UpscaylDigitalArt = '디지털 아트'
            AiModel_UpscaylRemacri = 'Remacri(비상업용)'
            AiModel_UpscaylUltramix = 'Ultramix(비상업용)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp(비상업용)'
            AiModelDescription_General = '대부분의 이미지에 잘 맞는 안정적인 기본 모델이며, 기본 선택값입니다.'
            AiModelDescription_Anime = '애니메이션, 일러스트, 선화 이미지에 적합합니다.'
            AiModelDescription_Lightweight = '더 빠르고 가벼우며, 품질 저하는 비교적 적습니다.'
            AiModelDescription_UpscaylStandard = '대부분의 이미지에 적합하며 전체적인 품질 균형이 좋습니다(권장)'
            AiModelDescription_UpscaylLite = '대부분의 이미지에 적합하며 더 빠르게 처리되고 품질 손실이 적습니다'
            AiModelDescription_UpscaylHighFidelity = '다양한 이미지에 잘 맞고, 디테일 보존과 질감의 부드러움을 중시합니다'
            AiModelDescription_UpscaylDigitalArt = '디지털 페인팅, 일러스트 등 아트 중심 이미지에 적합합니다'
            AiModelDescription_UpscaylRemacri = '자연 이미지에 적합하며 선명도와 디테일을 강화합니다'
            AiModelDescription_UpscaylUltramix = '자연 이미지에 적합하며 선명도와 세부 묘사의 균형을 맞춥니다'
            AiModelDescription_UpscaylUltrasharp = '자연 이미지에 적합하며 더 강한 선명도를 강조합니다'
            AiExecutionMode_Auto = '자동'
            AiExecutionMode_ForceCpu = 'CPU 강제'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'AI 향상을 진행 중입니다. 잠시만 기다려 주세요...'
            ConversionFeedbackTitle = 'AI 변환이 진행 중입니다. 잠시만 기다려 주세요'
            ConversionFeedbackDescription = 'Imvix Pro는 현재 작업을 계속 처리하고 있으며, 실패한 것이 아닙니다.'
            ConversionFeedbackHardwareHint = '처리 속도는 PC 하드웨어 성능에 따라 달라집니다. 성능이 높을수록 더 빠르고, 낮을수록 시간이 더 걸릴 수 있습니다.'
            ConversionFeedbackCloseHint = '변환이 끝날 때까지 Imvix Pro를 열어 둔 채 앱을 닫지 마세요.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '선택한 항목 {0}개는 AI 향상 대상이 아니므로 기존 변환 흐름으로 계속 처리됩니다.'
            AiModelFallbackToDefaultTemplate = '선택한 AI 모델 "{0}"을(를) 사용할 수 없어 "{1}"(으)로 자동 전환합니다.'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = '선택한 파일 형식은 AI 향상을 위한 전처리를 할 수 없습니다.'
            AiErrorPrepareTemporaryImage = 'AI 향상을 위한 임시 이미지를 준비할 수 없습니다.'
            AiErrorRuntimeFolderMissing = '번들된 AI 런타임 폴더가 없습니다. AI 디렉터리를 복원한 후 다시 시도하세요.'
            AiErrorRuntimeExecutableMissing = '번들된 AI 실행 파일이 없습니다. realesrgan-ncnn-vulkan.exe를 복원한 후 다시 시도하세요.'
            AiErrorModelMissingTemplate = '필요한 AI 모델 ''{0}'' 이(가) 없습니다.'
            AiErrorLightweightModelMissing = '필요한 경량 AI 모델 파일이 없습니다.'
            AiErrorCpuModeUnsupported = '번들된 AI 런타임은 CPU 모드를 지원하지 않습니다. GPU 모드를 "자동"으로 바꾸거나 AI 런타임 패키지를 업데이트하세요.'
            AiErrorModelLoadFailed = '필요한 AI 모델 파일을 불러올 수 없습니다.'
            AiErrorProcessExitCodeTemplate = 'AI 향상 프로세스가 종료 코드 {0}(으)로 실패했습니다.'
            AiErrorGpuAttemptFailedTemplate = 'GPU 시도 실패: {0}'
            AiErrorCpuFallbackFailedTemplate = 'CPU 폴백 실패: {0}'
            AiErrorPostResizeFailedTemplate = 'AI 출력 이미지를 목표 배율로 크기 조정할 수 없습니다: {0}'
        }
    }
    'de-DE' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'KI-Verbesserung'
            AiEnhancementToggleHint = 'Aktiviert die KI-Vorverarbeitung vor dem bisherigen Konvertierungsablauf.'
            AiEnhancementTab = 'KI-Verbesserung'
            AiEnhancementPanelTitle = 'KI-Verbesserung'
            AiEnhancementDescription = 'Wenn aktiviert, werden unterstützte Bilder zuerst per KI verbessert und danach an die vorhandene Konvertierung weitergegeben.'
            AiScale = 'Vergrößerungsfaktor'
            AiModel = 'Modell'
            AiExecutionMode = 'GPU-Modus'
            AiExecutionHint = 'Automatisch versucht zuerst Vulkan und fällt auf den CPU-Modus zurück, wenn die gebündelte Laufzeit das unterstützt.'
            AiInputSupportHint = 'Statische PNG-, JPG-, JPEG-, WEBP-, BMP-, TIFF- und einbildrige GIF-Dateien können verbessert werden. PDFs, PSDs, SVGs, animierte GIFs und andere Quellen bleiben im ursprünglichen Ablauf.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'Modellreihe: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Nur für nicht-kommerzielle Nutzung'
            AiModel_General = 'Allgemein'
            AiModel_Anime = 'Anime'
            AiModel_Lightweight = 'Leicht'
            AiModel_UpscaylStandard = 'Standard (Empfohlen)'
            AiModel_UpscaylLite = 'Lite'
            AiModel_UpscaylHighFidelity = 'Hohe Treue'
            AiModel_UpscaylDigitalArt = 'Digitale Kunst'
            AiModel_UpscaylRemacri = 'Remacri (Nicht kommerziell)'
            AiModel_UpscaylUltramix = 'Ultramix (Nicht kommerziell)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp (Nicht kommerziell)'
            AiModelDescription_General = 'Geeignet für die meisten Bilder, mit stabilen Allround-Ergebnissen und als Standardauswahl.'
            AiModelDescription_Anime = 'Optimiert für Anime, Illustrationen und Line-Art.'
            AiModelDescription_Lightweight = 'Schneller und ressourcenschonender, mit nur geringem Qualitätsverlust.'
            AiModelDescription_UpscaylStandard = 'Geeignet für die meisten Bilder und insgesamt ausgewogen (empfohlen)'
            AiModelDescription_UpscaylLite = 'Geeignet für die meisten Bilder, verarbeitet schneller und verliert nur wenig Qualität'
            AiModelDescription_UpscaylHighFidelity = 'Geeignet für viele Bildtypen und legt Wert auf Details und glattere Texturen'
            AiModelDescription_UpscaylDigitalArt = 'Geeignet für digitale Gemälde, Illustrationen und andere künstlerische Bilder'
            AiModelDescription_UpscaylRemacri = 'Geeignet für natürliche Bilder und verstärkt Schärfe sowie Details'
            AiModelDescription_UpscaylUltramix = 'Geeignet für natürliche Bilder und balanciert Schärfe und Detailwiedergabe'
            AiModelDescription_UpscaylUltrasharp = 'Geeignet für natürliche Bilder und betont eine stärkere Schärfung'
            AiExecutionMode_Auto = 'Automatisch'
            AiExecutionMode_ForceCpu = 'CPU erzwingen'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'Die KI-Verbesserung läuft. Bitte warten Sie...'
            ConversionFeedbackTitle = 'Die KI-Konvertierung läuft. Bitte haben Sie einen Moment Geduld.'
            ConversionFeedbackDescription = 'Imvix Pro verarbeitet die aktuelle Aufgabe weiterhin. Das bedeutet nicht, dass die Konvertierung fehlgeschlagen ist.'
            ConversionFeedbackHardwareHint = 'Die Verarbeitungsgeschwindigkeit hängt von der Hardware Ihres Computers ab. Leistungsstärkere Systeme sind meist schneller, schwächere benötigen mehr Zeit.'
            ConversionFeedbackCloseHint = 'Bitte lassen Sie Imvix Pro während der Konvertierung geöffnet und schließen Sie die App nicht.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} ausgewählte Elemente sind nicht für die KI-Verbesserung geeignet und laufen weiter durch den ursprünglichen Konvertierungsablauf.'
            AiModelFallbackToDefaultTemplate = 'Das ausgewählte KI-Modell "{0}" ist nicht verfügbar. Stattdessen wird "{1}" automatisch verwendet.'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = 'Der ausgewählte Dateityp kann nicht für die KI-Verbesserung vorbereitet werden.'
            AiErrorPrepareTemporaryImage = 'Für die KI-Verbesserung konnte kein temporäres Bild vorbereitet werden.'
            AiErrorRuntimeFolderMissing = 'Der gebündelte KI-Laufzeitordner fehlt. Stellen Sie das AI-Verzeichnis wieder her und versuchen Sie es erneut.'
            AiErrorRuntimeExecutableMissing = 'Die gebündelte KI-Laufzeitdatei fehlt. Stellen Sie realesrgan-ncnn-vulkan.exe wieder her und versuchen Sie es erneut.'
            AiErrorModelMissingTemplate = 'Das erforderliche KI-Modell ''{0}'' fehlt.'
            AiErrorLightweightModelMissing = 'Die erforderlichen leichten KI-Modelldateien fehlen.'
            AiErrorCpuModeUnsupported = 'Die gebündelte KI-Laufzeit unterstützt den CPU-Modus nicht. Wechseln Sie den GPU-Modus zu „Automatisch“ oder aktualisieren Sie das KI-Laufzeitpaket.'
            AiErrorModelLoadFailed = 'Die erforderlichen KI-Modelldateien konnten nicht geladen werden.'
            AiErrorProcessExitCodeTemplate = 'Der KI-Verbesserungsprozess ist mit dem Exitcode {0} fehlgeschlagen.'
            AiErrorGpuAttemptFailedTemplate = 'GPU-Versuch fehlgeschlagen: {0}'
            AiErrorCpuFallbackFailedTemplate = 'CPU-Fallback fehlgeschlagen: {0}'
            AiErrorPostResizeFailedTemplate = 'Die KI-Ausgabe konnte nicht auf die Zielskalierung angepasst werden: {0}'
        }
    }
    'fr-FR' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'Amélioration IA'
            AiEnhancementToggleHint = 'Active le prétraitement d''amélioration IA avant le flux de conversion existant.'
            AiEnhancementTab = 'Amélioration IA'
            AiEnhancementPanelTitle = 'Amélioration IA'
            AiEnhancementDescription = 'Lorsqu''elle est activée, les images compatibles sont d''abord améliorées par IA puis envoyées au pipeline de conversion existant.'
            AiScale = 'Facteur d''agrandissement'
            AiModel = 'Modèle'
            AiExecutionMode = 'Mode GPU'
            AiExecutionHint = 'Le mode automatique essaie d''abord Vulkan puis bascule vers le mode CPU si le runtime intégré le permet.'
            AiInputSupportHint = 'Les images statiques PNG, JPG, JPEG, WEBP, BMP, TIFF et GIF à image unique peuvent être améliorées. Les PDF, PSD, SVG, GIF animés et autres sources continuent dans le flux d''origine.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'Série : {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Utilisation non commerciale uniquement'
            AiModel_General = 'Général'
            AiModel_Anime = 'Anime'
            AiModel_Lightweight = 'Léger'
            AiModel_UpscaylStandard = 'Standard (Recommandé)'
            AiModel_UpscaylLite = 'Léger'
            AiModel_UpscaylHighFidelity = 'Haute fidélité'
            AiModel_UpscaylDigitalArt = 'Art numérique'
            AiModel_UpscaylRemacri = 'Remacri (Non commercial)'
            AiModel_UpscaylUltramix = 'Ultramix (Non commercial)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp (Non commercial)'
            AiModelDescription_General = 'Convient à la plupart des images, avec un rendu stable et équilibré, et reste le choix par défaut.'
            AiModelDescription_Anime = 'Optimisé pour les animes, les illustrations et le line art.'
            AiModelDescription_Lightweight = 'Plus rapide et plus léger, avec une perte de qualité limitée.'
            AiModelDescription_UpscaylStandard = 'Convient à la plupart des images, avec un résultat global équilibré (recommandé)'
            AiModelDescription_UpscaylLite = 'Convient à la plupart des images, traite plus vite et perd peu en qualité'
            AiModelDescription_UpscaylHighFidelity = 'Convient à de nombreux types d''images et privilégie les détails avec des textures plus douces'
            AiModelDescription_UpscaylDigitalArt = 'Adapté aux peintures numériques, illustrations et autres images artistiques'
            AiModelDescription_UpscaylRemacri = 'Adapté aux images naturelles, avec davantage de netteté et de détails'
            AiModelDescription_UpscaylUltramix = 'Adapté aux images naturelles, en équilibrant netteté et détails'
            AiModelDescription_UpscaylUltrasharp = 'Adapté aux images naturelles et orienté vers une netteté plus marquée'
            AiExecutionMode_Auto = 'Automatique'
            AiExecutionMode_ForceCpu = 'Forcer le CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'L''amélioration IA est en cours. Veuillez patienter...'
            ConversionFeedbackTitle = 'La conversion IA est en cours. Merci de patienter.'
            ConversionFeedbackDescription = 'Imvix Pro continue de traiter la tâche actuelle ; cela ne signifie pas que la conversion a échoué.'
            ConversionFeedbackHardwareHint = 'La vitesse de traitement dépend du matériel de votre ordinateur. Une configuration plus puissante est généralement plus rapide, tandis qu''une configuration plus modeste peut demander plus de temps.'
            ConversionFeedbackCloseHint = 'Pendant la conversion, laissez Imvix Pro ouvert et ne fermez pas l''application.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} éléments sélectionnés ne sont pas éligibles à l''amélioration IA et continueront dans le flux de conversion d''origine.'
            AiModelFallbackToDefaultTemplate = 'Le modèle IA sélectionné "{0}" n''est pas disponible, donc "{1}" sera utilisé automatiquement.'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = 'Le type de fichier sélectionné ne peut pas être préparé pour l''amélioration IA.'
            AiErrorPrepareTemporaryImage = 'Impossible de préparer une image temporaire pour l''amélioration IA.'
            AiErrorRuntimeFolderMissing = 'Le dossier du runtime IA intégré est introuvable. Restaurez le répertoire AI puis réessayez.'
            AiErrorRuntimeExecutableMissing = 'L''exécutable IA intégré est introuvable. Restaurez realesrgan-ncnn-vulkan.exe puis réessayez.'
            AiErrorModelMissingTemplate = 'Le modèle IA requis ''{0}'' est introuvable.'
            AiErrorLightweightModelMissing = 'Les fichiers du modèle IA léger requis sont introuvables.'
            AiErrorCpuModeUnsupported = 'Le runtime IA intégré ne prend pas en charge le mode CPU. Passez le mode GPU sur « Automatique » ou mettez à jour le paquet du runtime IA.'
            AiErrorModelLoadFailed = 'Les fichiers du modèle IA requis n''ont pas pu être chargés.'
            AiErrorProcessExitCodeTemplate = 'Le processus d''amélioration IA a échoué avec le code de sortie {0}.'
            AiErrorGpuAttemptFailedTemplate = 'Échec de la tentative GPU : {0}'
            AiErrorCpuFallbackFailedTemplate = 'Échec du repli CPU : {0}'
            AiErrorPostResizeFailedTemplate = 'Impossible de redimensionner la sortie IA au facteur cible : {0}'
        }
    }
    'it-IT' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'Miglioramento AI'
            AiEnhancementToggleHint = 'Attiva il pre-processo di miglioramento AI prima del flusso di conversione esistente.'
            AiEnhancementTab = 'Miglioramento AI'
            AiEnhancementPanelTitle = 'Miglioramento AI'
            AiEnhancementDescription = 'Quando è attivo, le immagini supportate vengono prima migliorate con l''AI e poi inviate alla pipeline di conversione esistente.'
            AiScale = 'Fattore di ingrandimento'
            AiModel = 'Modello'
            AiExecutionMode = 'Modalità GPU'
            AiExecutionHint = 'La modalità automatica prova prima Vulkan e passa alla modalità CPU quando il runtime incluso lo supporta.'
            AiInputSupportHint = 'È possibile migliorare immagini statiche PNG, JPG, JPEG, WEBP, BMP, TIFF e GIF a fotogramma singolo. PDF, PSD, SVG, GIF animate e altre sorgenti continuano nel flusso originale.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'Serie: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Solo per uso non commerciale'
            AiModel_General = 'Generale'
            AiModel_Anime = 'Anime'
            AiModel_Lightweight = 'Leggero'
            AiModel_UpscaylStandard = 'Standard (Consigliato)'
            AiModel_UpscaylLite = 'Lite'
            AiModel_UpscaylHighFidelity = 'Alta fedeltà'
            AiModel_UpscaylDigitalArt = 'Arte digitale'
            AiModel_UpscaylRemacri = 'Remacri (Non commerciale)'
            AiModel_UpscaylUltramix = 'Ultramix (Non commerciale)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp (Non commerciale)'
            AiModelDescription_General = 'Adatto alla maggior parte delle immagini, con risultati equilibrati e stabili, ed è la scelta predefinita.'
            AiModelDescription_Anime = 'Ottimizzato per anime, illustrazioni e line art.'
            AiModelDescription_Lightweight = 'Più rapido e leggero sulle risorse, con una perdita di qualità contenuta.'
            AiModelDescription_UpscaylStandard = 'Adatto alla maggior parte delle immagini, con un risultato complessivo bilanciato (consigliato)'
            AiModelDescription_UpscaylLite = 'Adatto alla maggior parte delle immagini, elabora più velocemente e perde poca qualità'
            AiModelDescription_UpscaylHighFidelity = 'Adatto a vari tipi di immagini e privilegia dettaglio e texture più morbide'
            AiModelDescription_UpscaylDigitalArt = 'Ideale per pittura digitale, illustrazioni e altre immagini artistiche'
            AiModelDescription_UpscaylRemacri = 'Ideale per immagini naturali, con maggiore nitidezza e dettaglio'
            AiModelDescription_UpscaylUltramix = 'Ideale per immagini naturali, bilanciando nitidezza e dettaglio'
            AiModelDescription_UpscaylUltrasharp = 'Ideale per immagini naturali e orientato a enfatizzare una nitidezza più forte'
            AiExecutionMode_Auto = 'Automatico'
            AiExecutionMode_ForceCpu = 'Forza CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'Miglioramento AI in corso. Attendi...'
            ConversionFeedbackTitle = 'La conversione AI è in corso. Attendi con pazienza.'
            ConversionFeedbackDescription = 'Imvix Pro sta ancora elaborando l''attività corrente e questo non significa che la conversione sia fallita.'
            ConversionFeedbackHardwareHint = 'La velocità di elaborazione dipende dall''hardware del computer. Un hardware più potente di solito è più veloce, mentre uno meno potente può richiedere più tempo.'
            ConversionFeedbackCloseHint = 'Durante la conversione, lascia Imvix Pro aperto e non chiudere l''app.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} elementi selezionati non sono idonei al miglioramento AI e continueranno nel flusso di conversione originale.'
            AiModelFallbackToDefaultTemplate = 'Il modello AI selezionato "{0}" non è disponibile, quindi verrà usato automaticamente "{1}".'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = 'Il tipo di file selezionato non può essere preparato per il miglioramento AI.'
            AiErrorPrepareTemporaryImage = 'Impossibile preparare un''immagine temporanea per il miglioramento AI.'
            AiErrorRuntimeFolderMissing = 'La cartella del runtime AI incluso è mancante. Ripristina la directory AI e riprova.'
            AiErrorRuntimeExecutableMissing = 'L''eseguibile AI incluso è mancante. Ripristina realesrgan-ncnn-vulkan.exe e riprova.'
            AiErrorModelMissingTemplate = 'Il modello AI richiesto ''{0}'' è mancante.'
            AiErrorLightweightModelMissing = 'Mancano i file richiesti del modello AI leggero.'
            AiErrorCpuModeUnsupported = 'Il runtime AI incluso non supporta la modalità CPU. Imposta la modalità GPU su "Automatico" oppure aggiorna il pacchetto del runtime AI.'
            AiErrorModelLoadFailed = 'Impossibile caricare i file del modello AI richiesto.'
            AiErrorProcessExitCodeTemplate = 'Il processo di miglioramento AI non è riuscito con codice di uscita {0}.'
            AiErrorGpuAttemptFailedTemplate = 'Tentativo GPU non riuscito: {0}'
            AiErrorCpuFallbackFailedTemplate = 'Fallback CPU non riuscito: {0}'
            AiErrorPostResizeFailedTemplate = 'Impossibile ridimensionare l''output AI al fattore richiesto: {0}'
        }
    }
    'ru-RU' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI-улучшение'
            AiEnhancementToggleHint = 'Включает предварительную AI-обработку перед существующим процессом конвертации.'
            AiEnhancementTab = 'AI-улучшение'
            AiEnhancementPanelTitle = 'AI-улучшение'
            AiEnhancementDescription = 'При включении поддерживаемые изображения сначала улучшаются с помощью AI, а затем передаются в существующий конвейер конвертации.'
            AiScale = 'Коэффициент увеличения'
            AiModel = 'Модель'
            AiExecutionMode = 'Режим GPU'
            AiExecutionHint = 'Автоматический режим сначала пробует Vulkan, а затем переключается на режим CPU, если встроенный runtime это поддерживает.'
            AiInputSupportHint = 'Можно улучшать статические PNG, JPG, JPEG, WEBP, BMP, TIFF и GIF с одним кадром. PDF, PSD, SVG, анимированные GIF и другие источники продолжают обрабатываться по исходному сценарию.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'Серия: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Только для некоммерческого использования'
            AiModel_General = 'Универсальный'
            AiModel_Anime = 'Аниме'
            AiModel_Lightweight = 'Легкая'
            AiModel_UpscaylStandard = 'Стандарт (Рекомендуется)'
            AiModel_UpscaylLite = 'Легкая'
            AiModel_UpscaylHighFidelity = 'Высокая точность'
            AiModel_UpscaylDigitalArt = 'Цифровое искусство'
            AiModel_UpscaylRemacri = 'Remacri (Некоммерческая)'
            AiModel_UpscaylUltramix = 'Ultramix (Некоммерческая)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp (Некоммерческая)'
            AiModelDescription_General = 'Подходит для большинства изображений, дает стабильный сбалансированный результат и используется по умолчанию.'
            AiModelDescription_Anime = 'Оптимизирована для аниме, иллюстраций и линейной графики.'
            AiModelDescription_Lightweight = 'Работает быстрее и легче по ресурсам, с небольшим компромиссом по качеству.'
            AiModelDescription_UpscaylStandard = 'Подходит для большинства изображений и дает сбалансированный общий результат (рекомендуется)'
            AiModelDescription_UpscaylLite = 'Подходит для большинства изображений, работает быстрее и почти не теряет в качестве'
            AiModelDescription_UpscaylHighFidelity = 'Подходит для разных типов изображений и делает акцент на деталях и более гладких текстурах'
            AiModelDescription_UpscaylDigitalArt = 'Подходит для цифровой живописи, иллюстраций и других художественных изображений'
            AiModelDescription_UpscaylRemacri = 'Подходит для естественных изображений, усиливая резкость и детализацию'
            AiModelDescription_UpscaylUltramix = 'Подходит для естественных изображений, балансируя резкость и детали'
            AiModelDescription_UpscaylUltrasharp = 'Подходит для естественных изображений и сильнее подчеркивает резкость'
            AiExecutionMode_Auto = 'Авто'
            AiExecutionMode_ForceCpu = 'Принудительно CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'AI-улучшение выполняется. Пожалуйста, подождите...'
            ConversionFeedbackTitle = 'AI-конвертация выполняется. Пожалуйста, наберитесь терпения.'
            ConversionFeedbackDescription = 'Imvix Pro продолжает обрабатывать текущую задачу, и это не означает, что конвертация завершилась с ошибкой.'
            ConversionFeedbackHardwareHint = 'Скорость обработки зависит от аппаратной конфигурации компьютера. Более мощное оборудование обычно работает быстрее, а менее мощное может потребовать больше времени.'
            ConversionFeedbackCloseHint = 'Во время конвертации оставьте Imvix Pro открытым и не закрывайте приложение.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} выбранных элементов не подходят для AI-улучшения и будут обработаны исходным конвейером конвертации.'
            AiModelFallbackToDefaultTemplate = 'Выбранная AI-модель "{0}" недоступна, поэтому автоматически будет использована "{1}".'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = 'Выбранный тип файла нельзя подготовить для AI-улучшения.'
            AiErrorPrepareTemporaryImage = 'Не удалось подготовить временное изображение для AI-улучшения.'
            AiErrorRuntimeFolderMissing = 'Отсутствует каталог встроенного AI-runtime. Восстановите каталог AI и повторите попытку.'
            AiErrorRuntimeExecutableMissing = 'Отсутствует встроенный AI-исполняемый файл. Восстановите realesrgan-ncnn-vulkan.exe и повторите попытку.'
            AiErrorModelMissingTemplate = 'Требуемая AI-модель ''{0}'' отсутствует.'
            AiErrorLightweightModelMissing = 'Отсутствуют файлы требуемой легкой AI-модели.'
            AiErrorCpuModeUnsupported = 'Встроенный AI-runtime не поддерживает режим CPU. Переключите режим GPU на «Авто» или обновите пакет AI-runtime.'
            AiErrorModelLoadFailed = 'Не удалось загрузить файлы требуемой AI-модели.'
            AiErrorProcessExitCodeTemplate = 'Процесс AI-улучшения завершился с ошибкой, код выхода: {0}.'
            AiErrorGpuAttemptFailedTemplate = 'Попытка GPU не удалась: {0}'
            AiErrorCpuFallbackFailedTemplate = 'Переход на CPU не удался: {0}'
            AiErrorPostResizeFailedTemplate = 'Не удалось изменить размер результата AI до целевого масштаба: {0}'
        }
    }
    'ar-SA' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'تحسين بالذكاء الاصطناعي'
            AiEnhancementToggleHint = 'يُفعّل المعالجة المسبقة للتحسين بالذكاء الاصطناعي قبل مسار التحويل الحالي.'
            AiEnhancementTab = 'تحسين بالذكاء الاصطناعي'
            AiEnhancementPanelTitle = 'تحسين بالذكاء الاصطناعي'
            AiEnhancementDescription = 'عند التفعيل، تُحسَّن الصور المدعومة أولاً بالذكاء الاصطناعي ثم تُمرَّر إلى مسار التحويل الحالي.'
            AiScale = 'نسبة التكبير'
            AiModel = 'النموذج'
            AiExecutionMode = 'وضع GPU'
            AiExecutionHint = 'يحاول الوضع التلقائي استخدام Vulkan أولاً، ثم يعود إلى وضع CPU إذا كانت الحزمة المضمنة تدعم ذلك.'
            AiInputSupportHint = 'يمكن تحسين صور PNG وJPG وJPEG وWEBP وBMP وTIFF وGIF الثابتة ذات الإطار الواحد. أما ملفات PDF وPSD وSVG وGIF المتحركة وغيرها فتستمر عبر المسار الأصلي.'
            AiModelSeries_RealEsrgan = 'Real-ESRGAN'
            AiModelSeries_Upscayl = 'Upscayl'
            AiModelSelectedSeriesTemplate = 'السلسلة: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ للاستخدام غير التجاري فقط'
            AiModel_General = 'عام'
            AiModel_Anime = 'أنمي'
            AiModel_Lightweight = 'خفيف'
            AiModel_UpscaylStandard = 'قياسي (موصى به)'
            AiModel_UpscaylLite = 'خفيف'
            AiModel_UpscaylHighFidelity = 'عالي الدقة'
            AiModel_UpscaylDigitalArt = 'فن رقمي'
            AiModel_UpscaylRemacri = 'Remacri (غير تجاري)'
            AiModel_UpscaylUltramix = 'Ultramix (غير تجاري)'
            AiModel_UpscaylUltrasharp = 'Ultrasharp (غير تجاري)'
            AiModelDescription_General = 'مناسب لمعظم الصور، بنتائج متوازنة ومستقرة، وهو الخيار الافتراضي.'
            AiModelDescription_Anime = 'مُحسَّن لصور الأنمي والرسوم التوضيحية والخطوط.'
            AiModelDescription_Lightweight = 'أسرع وأخف على العتاد، مع فقدان محدود في الجودة.'
            AiModelDescription_UpscaylStandard = 'مناسب لمعظم الصور، مع توازن جيد في النتيجة العامة (موصى به)'
            AiModelDescription_UpscaylLite = 'مناسب لمعظم الصور، أسرع في المعالجة مع فقدان بسيط في الجودة'
            AiModelDescription_UpscaylHighFidelity = 'مناسب لأنواع متعددة من الصور ويُركز على الحفاظ على التفاصيل مع نعومة أفضل للملمس'
            AiModelDescription_UpscaylDigitalArt = 'مناسب للرسوم الرقمية والرسوم التوضيحية وغيرها من الصور الفنية'
            AiModelDescription_UpscaylRemacri = 'مناسب للصور الطبيعية ويعزز الحدة والتفاصيل'
            AiModelDescription_UpscaylUltramix = 'مناسب للصور الطبيعية ويوازن بين الحدة والتفاصيل'
            AiModelDescription_UpscaylUltrasharp = 'مناسب للصور الطبيعية ويؤكد على تعزيز الحدة بشكل أكبر'
            AiExecutionMode_Auto = 'تلقائي'
            AiExecutionMode_ForceCpu = 'فرض CPU'
        }
        Status = [ordered]@{
            StatusAiEnhancing = 'يجري تحسين الصورة بالذكاء الاصطناعي. يُرجى الانتظار...'
            ConversionFeedbackTitle = 'التحويل بالذكاء الاصطناعي جارٍ. يُرجى التحلي بالصبر.'
            ConversionFeedbackDescription = 'يواصل Imvix Pro معالجة المهمة الحالية، وهذا لا يعني أن التحويل قد فشل.'
            ConversionFeedbackHardwareHint = 'تعتمد سرعة المعالجة على عتاد جهاز الكمبيوتر لديك. فكلما كانت المواصفات أعلى كانت السرعة أكبر، بينما قد تحتاج الأجهزة الأضعف إلى وقت أطول.'
            ConversionFeedbackCloseHint = 'أبقِ Imvix Pro مفتوحًا أثناء التحويل ولا تغلق التطبيق.'
        }
        Warning = [ordered]@{
            WarningAiUnsupportedInputsTemplate = '{0} من العناصر المحددة غير مؤهلة لتحسين الذكاء الاصطناعي وستستمر عبر مسار التحويل الأصلي.'
            AiModelFallbackToDefaultTemplate = 'نموذج الذكاء الاصطناعي المحدد "{0}" غير متاح، لذلك سيتم استخدام "{1}" تلقائيًا.'
        }
        Errors = [ordered]@{
            AiErrorNormalizeUnsupported = 'لا يمكن تهيئة نوع الملف المحدد لتحسين الذكاء الاصطناعي.'
            AiErrorPrepareTemporaryImage = 'تعذر تجهيز صورة مؤقتة لتحسين الذكاء الاصطناعي.'
            AiErrorRuntimeFolderMissing = 'مجلد وقت تشغيل الذكاء الاصطناعي المضمّن مفقود. أعد استعادة دليل AI ثم حاول مرة أخرى.'
            AiErrorRuntimeExecutableMissing = 'ملف تشغيل الذكاء الاصطناعي المضمّن مفقود. أعد استعادة realesrgan-ncnn-vulkan.exe ثم حاول مرة أخرى.'
            AiErrorModelMissingTemplate = 'نموذج الذكاء الاصطناعي المطلوب ''{0}'' مفقود.'
            AiErrorLightweightModelMissing = 'ملفات نموذج الذكاء الاصطناعي الخفيف المطلوبة مفقودة.'
            AiErrorCpuModeUnsupported = 'وقت تشغيل الذكاء الاصطناعي المضمّن لا يدعم وضع CPU. بدّل وضع GPU إلى "تلقائي" أو حدّث حزمة وقت التشغيل.'
            AiErrorModelLoadFailed = 'تعذر تحميل ملفات نموذج الذكاء الاصطناعي المطلوبة.'
            AiErrorProcessExitCodeTemplate = 'فشلت عملية تحسين الذكاء الاصطناعي برمز الخروج {0}.'
            AiErrorGpuAttemptFailedTemplate = 'فشلت محاولة GPU: {0}'
            AiErrorCpuFallbackFailedTemplate = 'فشل الرجوع إلى CPU: {0}'
            AiErrorPostResizeFailedTemplate = 'تعذّر تغيير حجم ناتج الذكاء الاصطناعي إلى مقياس التكبير المطلوب: {0}'
        }
    }
}

$managedKeys = [HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($sectionName in @('Start', 'Status', 'Warning', 'Errors')) {
    foreach ($key in $translations['en-US'][$sectionName].Keys) {
        [void]$managedKeys.Add($key)
    }
}

function Add-Entry {
    param(
        [List[object]]$Entries,
        [string]$Key,
        [string]$Value
    )

    $Entries.Add([pscustomobject]@{
            Key = $Key
            Value = $Value
        }) | Out-Null
}

function Add-TranslationBlock {
    param(
        [List[object]]$Entries,
        [System.Collections.Specialized.OrderedDictionary]$Block
    )

    foreach ($item in $Block.GetEnumerator()) {
        Add-Entry -Entries $Entries -Key $item.Key -Value ([string]$item.Value)
    }
}

function Format-JsonLine {
    param(
        [string]$Key,
        [string]$Value,
        [bool]$HasComma
    )

    $encodedKey = ($Key | ConvertTo-Json -Compress)
    $encodedValue = ($Value | ConvertTo-Json -Compress)
    $suffix = if ($HasComma) { ',' } else { '' }
    return "    ${encodedKey}:  ${encodedValue}${suffix}"
}

foreach ($languageCode in $translations.Keys) {
    $baseContent = git show "HEAD:Assets/Localization/$languageCode.json"
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to load HEAD content for $languageCode."
    }

    $json = ($baseContent -join "`n")
    $document = $json | ConvertFrom-Json
    $entries = [List[object]]::new()

    $startInserted = $false
    $statusInserted = $false
    $warningInserted = $false
    $errorsInserted = $false

    foreach ($property in $document.PSObject.Properties) {
        if ($managedKeys.Contains($property.Name)) {
            continue
        }

        Add-Entry -Entries $entries -Key $property.Name -Value ([string]$property.Value)

        switch ($property.Name) {
            'StartConversion' {
                Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Start
                $startInserted = $true
            }
            'StatusConverting' {
                Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Status
                $statusInserted = $true
            }
            'WarningGifFramesTooManyTemplate' {
                Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Warning
                $warningInserted = $true
            }
            'UnknownReason' {
                Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Errors
                $errorsInserted = $true
            }
        }
    }

    if (-not $startInserted) {
        Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Start
    }

    if (-not $statusInserted) {
        Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Status
    }

    if (-not $warningInserted) {
        Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Warning
    }

    if (-not $errorsInserted) {
        Add-TranslationBlock -Entries $entries -Block $translations[$languageCode].Errors
    }

    $lines = [List[string]]::new()
    $lines.Add('{') | Out-Null

    for ($index = 0; $index -lt $entries.Count; $index++) {
        $entry = $entries[$index]
        $lines.Add((Format-JsonLine -Key $entry.Key -Value $entry.Value -HasComma ($index -lt ($entries.Count - 1)))) | Out-Null
    }

    $lines.Add('}') | Out-Null

    $path = Join-Path (Get-Location) "Assets/Localization/$languageCode.json"
    [File]::WriteAllText($path, ($lines -join "`r`n") + "`r`n", [UTF8Encoding]::new($true))
}
