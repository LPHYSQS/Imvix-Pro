using namespace System.Collections.Generic
using namespace System.IO
using namespace System.Text

$translations = [ordered]@{
    'en-US' = [ordered]@{
        Start = [ordered]@{
            AiEnhancementToggle = 'AI Enhancement'
            AiEnhancementToggleHint = 'Enable AI enhancement preprocessing before the original conversion flow.'
            AiEnhancementTab = 'AI Tools'
            AiEnhancementPanelTitle = 'AI Enhancement'
            AiEnhancementDescription = 'When enabled, supported images are enhanced first and then passed to the existing conversion pipeline.'
            AiScale = 'Upscale Ratio'
            AiModel = 'Model'
            AiExecutionMode = 'GPU Mode'
            AiExecutionHint = 'Auto tries Vulkan first and falls back to CPU mode when the bundled runtime supports it.'
            AiInputSupportHint = 'Static PNG, JPG, JPEG, WEBP, BMP, TIFF, and single-frame GIF images can be enhanced. PDFs, PSDs, SVGs, animated GIFs, and other sources continue through the original flow.'
            AiModelSeries_RealEsrgan = 'Basic Enhancement'
            AiModelSeries_Upscayl = 'Scene-Tuned'
            AiModelSelectedSeriesTemplate = 'Series: {0}'
            AiModelSelectedRestriction_NonCommercial = 'Warning: Non-commercial use only'
            AiModel_General = 'Everyday Photo'
            AiModel_Anime = 'Anime & Illustration'
            AiModel_Lightweight = 'Fast & Lightweight'
            AiModel_UpscaylStandard = 'Balanced Enhancement (Recommended)'
            AiModel_UpscaylLite = 'Fast Enhancement'
            AiModel_UpscaylHighFidelity = 'Detail Preservation'
            AiModel_UpscaylDigitalArt = 'Art & Illustration'
            AiModel_UpscaylRemacri = 'Natural Photo Detail (Non-commercial)'
            AiModel_UpscaylUltramix = 'Natural Photo Balanced (Non-commercial)'
            AiModel_UpscaylUltrasharp = 'Extra Sharp (Non-commercial)'
            AiModelDescription_General = 'Best for daily photos and screenshots, with stable results for most non-illustration images.'
            AiModelDescription_Anime = 'Best for anime, comics, illustrations, and clean line art.'
            AiModelDescription_Lightweight = 'Uses less hardware and finishes faster, suitable for older PCs or quick tasks.'
            AiModelDescription_UpscaylStandard = 'Recommended for most images, balancing sharpness, detail, and texture.'
            AiModelDescription_UpscaylLite = 'Better when speed matters, keeping a clean overall result with shorter processing time.'
            AiModelDescription_UpscaylHighFidelity = 'Keeps more small details and texture, suitable for detailed photos and close-ups.'
            AiModelDescription_UpscaylDigitalArt = 'Best for posters, concept art, drawings, and stylized artwork.'
            AiModelDescription_UpscaylRemacri = 'Adds stronger detail recovery to natural photos. Non-commercial use only.'
            AiModelDescription_UpscaylUltramix = 'Balances detail and sharpness for natural photos. Non-commercial use only.'
            AiModelDescription_UpscaylUltrasharp = 'The sharpest option for natural photos. Non-commercial use only.'
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
            AiEnhancementTab = 'AI工具'
            AiEnhancementPanelTitle = 'AI增强'
            AiEnhancementDescription = '启用后，受支持的图片会先进行 AI 增强，再交给现有转换流程处理。'
            AiScale = '放大倍率'
            AiModel = '模型选择'
            AiExecutionMode = 'GPU模式'
            AiExecutionHint = '自动模式会优先尝试 Vulkan；如果当前捆绑运行时支持，再回退到 CPU 模式。'
            AiInputSupportHint = '支持静态 PNG、JPG、JPEG、WEBP、BMP、TIFF 和单帧 GIF 图片增强。PDF、PSD、SVG、动态图 GIF 等文件会继续走原有流程。'
            AiModelSeries_RealEsrgan = '基础增强'
            AiModelSeries_Upscayl = '场景优化'
            AiModelSelectedSeriesTemplate = '系列：{0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 仅限非商业用途'
            AiModel_General = '日常照片'
            AiModel_Anime = '动漫插画'
            AiModel_Lightweight = '快速轻量'
            AiModel_UpscaylStandard = '均衡增强（推荐）'
            AiModel_UpscaylLite = '快速增强'
            AiModel_UpscaylHighFidelity = '细节保留'
            AiModel_UpscaylDigitalArt = '艺术插画'
            AiModel_UpscaylRemacri = '自然照片细节（非商业）'
            AiModel_UpscaylUltramix = '自然照片均衡（非商业）'
            AiModel_UpscaylUltrasharp = '强锐化（非商业）'
            AiModelDescription_General = '适合日常照片与截图，大多数非插画类图像都能获得稳定效果。'
            AiModelDescription_Anime = '适合动漫、漫画、插画和线稿清晰的图像。'
            AiModelDescription_Lightweight = '更省硬件资源，处理速度更快，适合老电脑或快速任务。'
            AiModelDescription_UpscaylStandard = '适合大多数图像，是默认推荐选项，在锐度、细节和纹理之间更均衡。'
            AiModelDescription_UpscaylLite = '更适合看重速度的场景，在缩短处理时间的同时保持较干净的整体效果。'
            AiModelDescription_UpscaylHighFidelity = '更注重保留细小细节和纹理，适合细节较多的照片与特写。'
            AiModelDescription_UpscaylDigitalArt = '适合海报、概念图、绘画和风格化插画。'
            AiModelDescription_UpscaylRemacri = '适合自然照片，细节恢复更强。仅限非商业用途。'
            AiModelDescription_UpscaylUltramix = '适合自然照片，在细节和锐度之间更均衡。仅限非商业用途。'
            AiModelDescription_UpscaylUltrasharp = '适合自然照片，锐化效果最强。仅限非商业用途。'
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
            AiEnhancementTab = 'AI工具'
            AiEnhancementPanelTitle = 'AI增強'
            AiEnhancementDescription = '啟用後，受支援的圖片會先進行 AI 增強，再交給現有轉換流程處理。'
            AiScale = '放大倍率'
            AiModel = '模型選擇'
            AiExecutionMode = 'GPU模式'
            AiExecutionHint = '自動模式會優先嘗試 Vulkan；如果目前捆綁執行環境支援，再回退到 CPU 模式。'
            AiInputSupportHint = '支援靜態 PNG、JPG、JPEG、WEBP、BMP、TIFF 與單幀 GIF 圖片增強。PDF、PSD、SVG、動態 GIF 等檔案會繼續走原有流程。'
            AiModelSeries_RealEsrgan = '基礎增強'
            AiModelSeries_Upscayl = '場景優化'
            AiModelSelectedSeriesTemplate = '系列：{0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 僅限非商業用途'
            AiModel_General = '日常照片'
            AiModel_Anime = '動漫插畫'
            AiModel_Lightweight = '快速輕量'
            AiModel_UpscaylStandard = '均衡增強（推薦）'
            AiModel_UpscaylLite = '快速增強'
            AiModel_UpscaylHighFidelity = '細節保留'
            AiModel_UpscaylDigitalArt = '藝術插畫'
            AiModel_UpscaylRemacri = '自然照片細節（非商業）'
            AiModel_UpscaylUltramix = '自然照片均衡（非商業）'
            AiModel_UpscaylUltrasharp = '強銳化（非商業）'
            AiModelDescription_General = '適合日常照片與截圖，大多數非插畫類圖像都能得到穩定效果。'
            AiModelDescription_Anime = '適合動漫、漫畫、插畫與線稿清楚的圖像。'
            AiModelDescription_Lightweight = '更省硬體資源，處理速度更快，適合舊電腦或快速任務。'
            AiModelDescription_UpscaylStandard = '適合大多數圖像，是預設推薦選項，在銳利度、細節與紋理之間更均衡。'
            AiModelDescription_UpscaylLite = '更適合重視速度的場景，在縮短處理時間的同時保持較乾淨的整體效果。'
            AiModelDescription_UpscaylHighFidelity = '更著重保留細小細節與紋理，適合細節較多的照片與特寫。'
            AiModelDescription_UpscaylDigitalArt = '適合海報、概念圖、繪畫與風格化插畫。'
            AiModelDescription_UpscaylRemacri = '適合自然照片，細節恢復更強。僅限非商業用途。'
            AiModelDescription_UpscaylUltramix = '適合自然照片，在細節與銳利度之間更均衡。僅限非商業用途。'
            AiModelDescription_UpscaylUltrasharp = '適合自然照片，銳化效果最強。僅限非商業用途。'
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
            AiEnhancementTab = 'AIツール'
            AiEnhancementPanelTitle = 'AI高画質化'
            AiEnhancementDescription = '有効時は、対応画像を先に AI で高画質化してから既存の変換パイプラインへ渡します。'
            AiScale = '拡大倍率'
            AiModel = 'モデル'
            AiExecutionMode = 'GPUモード'
            AiExecutionHint = '自動はまず Vulkan を試し、同梱ランタイムが対応していれば CPU モードにフォールバックします。'
            AiInputSupportHint = '静止画の PNG、JPG、JPEG、WEBP、BMP、TIFF、および単一フレーム GIF を高画質化できます。PDF、PSD、SVG、アニメーション GIF などは従来フローのまま処理されます。'
            AiModelSeries_RealEsrgan = '基本強化'
            AiModelSeries_Upscayl = 'シーン最適化'
            AiModelSelectedSeriesTemplate = 'シリーズ: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 非商用利用に限ります'
            AiModel_General = '日常写真'
            AiModel_Anime = 'アニメ・イラスト'
            AiModel_Lightweight = '高速・軽量'
            AiModel_UpscaylStandard = 'バランス強化（推奨）'
            AiModel_UpscaylLite = '高速強化'
            AiModel_UpscaylHighFidelity = 'ディテール保持'
            AiModel_UpscaylDigitalArt = 'アート・イラスト'
            AiModel_UpscaylRemacri = '自然写真ディテール（非商用）'
            AiModel_UpscaylUltramix = '自然写真バランス（非商用）'
            AiModel_UpscaylUltrasharp = '強シャープ（非商用）'
            AiModelDescription_General = '日常写真やスクリーンショット向けで、イラスト以外の多くの画像に安定して使えます。'
            AiModelDescription_Anime = 'アニメ、漫画、イラスト、線画がはっきりした画像に向いています。'
            AiModelDescription_Lightweight = 'ハードウェア負荷が軽く処理も速いため、古めの PC や簡易処理に向いています。'
            AiModelDescription_UpscaylStandard = '多くの画像に向く推奨モデルで、シャープさ・細部・質感のバランスが良好です。'
            AiModelDescription_UpscaylLite = '速度を重視したいときに向いており、短い処理時間で全体をすっきり仕上げます。'
            AiModelDescription_UpscaylHighFidelity = '細かなディテールや質感を残しやすく、情報量の多い写真や接写に向いています。'
            AiModelDescription_UpscaylDigitalArt = 'ポスター、コンセプトアート、イラスト、スタイライズ表現に向いています。'
            AiModelDescription_UpscaylRemacri = '自然写真の細部をより強く引き出します。非商用利用のみ。'
            AiModelDescription_UpscaylUltramix = '自然写真で細部とシャープさのバランスを取りたいとき向けです。非商用利用のみ。'
            AiModelDescription_UpscaylUltrasharp = '自然写真向けで、最も強くシャープさを出します。非商用利用のみ。'
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
            AiEnhancementTab = 'AI 도구'
            AiEnhancementPanelTitle = 'AI 향상'
            AiEnhancementDescription = '활성화하면 지원되는 이미지를 먼저 AI로 향상한 뒤 기존 변환 파이프라인으로 전달합니다.'
            AiScale = '확대 배율'
            AiModel = '모델'
            AiExecutionMode = 'GPU 모드'
            AiExecutionHint = '자동 모드는 먼저 Vulkan을 시도하고, 번들 런타임이 지원하면 CPU 모드로 전환합니다.'
            AiInputSupportHint = '정지 이미지 PNG, JPG, JPEG, WEBP, BMP, TIFF 및 단일 프레임 GIF를 향상할 수 있습니다. PDF, PSD, SVG, 애니메이션 GIF 등은 기존 흐름으로 계속 처리됩니다.'
            AiModelSeries_RealEsrgan = '기본 강화'
            AiModelSeries_Upscayl = '장면 최적화'
            AiModelSelectedSeriesTemplate = '시리즈: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ 비상업적 용도로만 사용 가능'
            AiModel_General = '일반 사진'
            AiModel_Anime = '애니메이션·일러스트'
            AiModel_Lightweight = '빠름·경량'
            AiModel_UpscaylStandard = '균형 강화(권장)'
            AiModel_UpscaylLite = '빠른 강화'
            AiModel_UpscaylHighFidelity = '디테일 유지'
            AiModel_UpscaylDigitalArt = '아트·일러스트'
            AiModel_UpscaylRemacri = '자연 사진 디테일(비상업용)'
            AiModel_UpscaylUltramix = '자연 사진 균형(비상업용)'
            AiModel_UpscaylUltrasharp = '강한 선명도(비상업용)'
            AiModelDescription_General = '일상 사진과 스크린샷에 잘 맞고, 일러스트가 아닌 대부분의 이미지에서 안정적인 결과를 냅니다.'
            AiModelDescription_Anime = '애니메이션, 만화, 일러스트, 선이 또렷한 이미지에 잘 맞습니다.'
            AiModelDescription_Lightweight = '하드웨어 부담이 적고 더 빠르게 끝나므로 오래된 PC나 빠른 작업에 적합합니다.'
            AiModelDescription_UpscaylStandard = '대부분의 이미지에 맞는 기본 추천 모델로, 선명도·디테일·질감의 균형이 좋습니다.'
            AiModelDescription_UpscaylLite = '속도가 중요할 때 적합하며, 처리 시간을 줄이면서 전체 결과를 깔끔하게 유지합니다.'
            AiModelDescription_UpscaylHighFidelity = '작은 디테일과 질감을 더 잘 살려, 디테일이 많은 사진과 근접 촬영에 적합합니다.'
            AiModelDescription_UpscaylDigitalArt = '포스터, 콘셉트 아트, 드로잉, 스타일화된 일러스트에 적합합니다.'
            AiModelDescription_UpscaylRemacri = '자연 사진의 디테일 복원을 더 강하게 해줍니다. 비상업용으로만 사용할 수 있습니다.'
            AiModelDescription_UpscaylUltramix = '자연 사진에서 디테일과 선명도의 균형을 맞춥니다. 비상업용으로만 사용할 수 있습니다.'
            AiModelDescription_UpscaylUltrasharp = '자연 사진용 중 가장 강한 선명도를 제공합니다. 비상업용으로만 사용할 수 있습니다.'
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
            AiEnhancementTab = 'KI-Tools'
            AiEnhancementPanelTitle = 'KI-Verbesserung'
            AiEnhancementDescription = 'Wenn aktiviert, werden unterstützte Bilder zuerst per KI verbessert und danach an die vorhandene Konvertierung weitergegeben.'
            AiScale = 'Vergrößerungsfaktor'
            AiModel = 'Modell'
            AiExecutionMode = 'GPU-Modus'
            AiExecutionHint = 'Automatisch versucht zuerst Vulkan und fällt auf den CPU-Modus zurück, wenn die gebündelte Laufzeit das unterstützt.'
            AiInputSupportHint = 'Statische PNG-, JPG-, JPEG-, WEBP-, BMP-, TIFF- und einbildrige GIF-Dateien können verbessert werden. PDFs, PSDs, SVGs, animierte GIFs und andere Quellen bleiben im ursprünglichen Ablauf.'
            AiModelSeries_RealEsrgan = 'Basisverbesserung'
            AiModelSeries_Upscayl = 'Szenenoptimiert'
            AiModelSelectedSeriesTemplate = 'Modellreihe: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Nur für nicht-kommerzielle Nutzung'
            AiModel_General = 'Alltagsfoto'
            AiModel_Anime = 'Anime und Illustration'
            AiModel_Lightweight = 'Schnell und leicht'
            AiModel_UpscaylStandard = 'Ausgewogene Verbesserung (empfohlen)'
            AiModel_UpscaylLite = 'Schnelle Verbesserung'
            AiModel_UpscaylHighFidelity = 'Detailerhalt'
            AiModel_UpscaylDigitalArt = 'Kunst & Illustration'
            AiModel_UpscaylRemacri = 'Naturfoto-Details (nicht kommerziell)'
            AiModel_UpscaylUltramix = 'Naturfoto ausgewogen (nicht kommerziell)'
            AiModel_UpscaylUltrasharp = 'Extra scharf (nicht kommerziell)'
            AiModelDescription_General = 'Gut für Alltagsfotos und Screenshots und liefert bei den meisten nicht-illustrativen Bildern stabile Ergebnisse.'
            AiModelDescription_Anime = 'Geeignet für Anime, Comics, Illustrationen und saubere Line-Art.'
            AiModelDescription_Lightweight = 'Benötigt weniger Hardware und ist schneller fertig, ideal für ältere PCs oder schnelle Aufgaben.'
            AiModelDescription_UpscaylStandard = 'Empfohlene Standardwahl für die meisten Bilder, mit guter Balance aus Schärfe, Details und Textur.'
            AiModelDescription_UpscaylLite = 'Sinnvoll, wenn Geschwindigkeit wichtiger ist, und hält das Ergebnis bei kürzerer Laufzeit sauber.'
            AiModelDescription_UpscaylHighFidelity = 'Erhält mehr kleine Details und Texturen, gut für detailreiche Fotos und Nahaufnahmen.'
            AiModelDescription_UpscaylDigitalArt = 'Geeignet für Poster, Concept Art, Zeichnungen und stilisierte Illustrationen.'
            AiModelDescription_UpscaylRemacri = 'Holt bei Naturfotos stärkere Details heraus. Nur für nicht kommerzielle Nutzung.'
            AiModelDescription_UpscaylUltramix = 'Balanciert Details und Schärfe bei Naturfotos. Nur für nicht kommerzielle Nutzung.'
            AiModelDescription_UpscaylUltrasharp = 'Die schärfste Option für Naturfotos. Nur für nicht kommerzielle Nutzung.'
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
            AiEnhancementTab = 'Outils IA'
            AiEnhancementPanelTitle = 'Amélioration IA'
            AiEnhancementDescription = 'Lorsqu''elle est activée, les images compatibles sont d''abord améliorées par IA puis envoyées au pipeline de conversion existant.'
            AiScale = 'Facteur d''agrandissement'
            AiModel = 'Modèle'
            AiExecutionMode = 'Mode GPU'
            AiExecutionHint = 'Le mode automatique essaie d''abord Vulkan puis bascule vers le mode CPU si le runtime intégré le permet.'
            AiInputSupportHint = 'Les images statiques PNG, JPG, JPEG, WEBP, BMP, TIFF et GIF à image unique peuvent être améliorées. Les PDF, PSD, SVG, GIF animés et autres sources continuent dans le flux d''origine.'
            AiModelSeries_RealEsrgan = 'Amélioration de base'
            AiModelSeries_Upscayl = 'Optimisé par scène'
            AiModelSelectedSeriesTemplate = 'Série : {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Utilisation non commerciale uniquement'
            AiModel_General = 'Photo du quotidien'
            AiModel_Anime = 'Anime et illustration'
            AiModel_Lightweight = 'Rapide et léger'
            AiModel_UpscaylStandard = 'Amélioration équilibrée (recommandée)'
            AiModel_UpscaylLite = 'Amélioration rapide'
            AiModel_UpscaylHighFidelity = 'Préservation des détails'
            AiModel_UpscaylDigitalArt = 'Art et illustration'
            AiModel_UpscaylRemacri = 'Détail photo naturelle (non commercial)'
            AiModel_UpscaylUltramix = 'Photo naturelle équilibrée (non commercial)'
            AiModel_UpscaylUltrasharp = 'Netteté renforcée (non commercial)'
            AiModelDescription_General = 'Idéal pour les photos du quotidien et les captures d''écran, avec un résultat stable sur la plupart des images non illustrées.'
            AiModelDescription_Anime = 'Idéal pour les images d''anime, les bandes dessinées, les illustrations et les line arts nets.'
            AiModelDescription_Lightweight = 'Demande moins de ressources et termine plus vite, pratique sur un PC ancien ou pour un traitement rapide.'
            AiModelDescription_UpscaylStandard = 'Modèle recommandé par défaut pour la plupart des images, avec un bon équilibre entre netteté, détails et texture.'
            AiModelDescription_UpscaylLite = 'À privilégier quand la vitesse compte, avec un rendu propre et un temps de traitement plus court.'
            AiModelDescription_UpscaylHighFidelity = 'Préserve davantage les petits détails et les textures, pratique pour les photos détaillées et les gros plans.'
            AiModelDescription_UpscaylDigitalArt = 'Idéal pour les affiches, le concept art, les dessins et les visuels stylisés.'
            AiModelDescription_UpscaylRemacri = 'Renforce davantage les détails sur les photos naturelles. Utilisation non commerciale uniquement.'
            AiModelDescription_UpscaylUltramix = 'Équilibre détails et netteté sur les photos naturelles. Utilisation non commerciale uniquement.'
            AiModelDescription_UpscaylUltrasharp = 'L''option la plus nette pour les photos naturelles. Utilisation non commerciale uniquement.'
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
            AiEnhancementTab = 'Strumenti AI'
            AiEnhancementPanelTitle = 'Miglioramento AI'
            AiEnhancementDescription = 'Quando è attivo, le immagini supportate vengono prima migliorate con l''AI e poi inviate alla pipeline di conversione esistente.'
            AiScale = 'Fattore di ingrandimento'
            AiModel = 'Modello'
            AiExecutionMode = 'Modalità GPU'
            AiExecutionHint = 'La modalità automatica prova prima Vulkan e passa alla modalità CPU quando il runtime incluso lo supporta.'
            AiInputSupportHint = 'È possibile migliorare immagini statiche PNG, JPG, JPEG, WEBP, BMP, TIFF e GIF a fotogramma singolo. PDF, PSD, SVG, GIF animate e altre sorgenti continuano nel flusso originale.'
            AiModelSeries_RealEsrgan = 'Miglioramento base'
            AiModelSeries_Upscayl = 'Ottimizzato per scena'
            AiModelSelectedSeriesTemplate = 'Serie: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Solo per uso non commerciale'
            AiModel_General = 'Foto quotidiana'
            AiModel_Anime = 'Anime e illustrazione'
            AiModel_Lightweight = 'Rapido e leggero'
            AiModel_UpscaylStandard = 'Miglioramento bilanciato (consigliato)'
            AiModel_UpscaylLite = 'Miglioramento rapido'
            AiModel_UpscaylHighFidelity = 'Conservazione dei dettagli'
            AiModel_UpscaylDigitalArt = 'Arte e illustrazione'
            AiModel_UpscaylRemacri = 'Dettaglio foto naturale (non commerciale)'
            AiModel_UpscaylUltramix = 'Foto naturale bilanciata (non commerciale)'
            AiModel_UpscaylUltrasharp = 'Nitidezza extra (non commerciale)'
            AiModelDescription_General = 'Adatto a foto quotidiane e schermate, con risultati stabili sulla maggior parte delle immagini non illustrative.'
            AiModelDescription_Anime = 'Ideale per anime, fumetti, illustrazioni e line art pulite.'
            AiModelDescription_Lightweight = 'Richiede meno risorse e termina più in fretta, utile su PC datati o per lavori rapidi.'
            AiModelDescription_UpscaylStandard = 'Modello consigliato predefinito per la maggior parte delle immagini, con buon equilibrio tra nitidezza, dettaglio e texture.'
            AiModelDescription_UpscaylLite = 'Più adatto quando conta la velocità, mantenendo un risultato pulito in meno tempo.'
            AiModelDescription_UpscaylHighFidelity = 'Conserva meglio i piccoli dettagli e le texture, utile per foto ricche di dettagli e primi piani.'
            AiModelDescription_UpscaylDigitalArt = 'Ideale per poster, concept art, disegni e illustrazioni stilizzate.'
            AiModelDescription_UpscaylRemacri = 'Recupera più dettagli nelle foto naturali. Solo per uso non commerciale.'
            AiModelDescription_UpscaylUltramix = 'Bilancia dettaglio e nitidezza nelle foto naturali. Solo per uso non commerciale.'
            AiModelDescription_UpscaylUltrasharp = 'L''opzione più incisiva per la nitidezza nelle foto naturali. Solo per uso non commerciale.'
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
            AiEnhancementTab = 'AI-инструменты'
            AiEnhancementPanelTitle = 'AI-улучшение'
            AiEnhancementDescription = 'При включении поддерживаемые изображения сначала улучшаются с помощью AI, а затем передаются в существующий конвейер конвертации.'
            AiScale = 'Коэффициент увеличения'
            AiModel = 'Модель'
            AiExecutionMode = 'Режим GPU'
            AiExecutionHint = 'Автоматический режим сначала пробует Vulkan, а затем переключается на режим CPU, если встроенный runtime это поддерживает.'
            AiInputSupportHint = 'Можно улучшать статические PNG, JPG, JPEG, WEBP, BMP, TIFF и GIF с одним кадром. PDF, PSD, SVG, анимированные GIF и другие источники продолжают обрабатываться по исходному сценарию.'
            AiModelSeries_RealEsrgan = 'Базовое улучшение'
            AiModelSeries_Upscayl = 'Оптимизация по сцене'
            AiModelSelectedSeriesTemplate = 'Серия: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ Только для некоммерческого использования'
            AiModel_General = 'Повседневное фото'
            AiModel_Anime = 'Аниме и иллюстрации'
            AiModel_Lightweight = 'Быстрый и легкий'
            AiModel_UpscaylStandard = 'Сбалансированное улучшение (рекомендуется)'
            AiModel_UpscaylLite = 'Быстрое улучшение'
            AiModel_UpscaylHighFidelity = 'Сохранение деталей'
            AiModel_UpscaylDigitalArt = 'Арт и иллюстрации'
            AiModel_UpscaylRemacri = 'Детали естественного фото (некоммерческое)'
            AiModel_UpscaylUltramix = 'Естественное фото, баланс (некоммерческое)'
            AiModel_UpscaylUltrasharp = 'Экстра-резкость (некоммерческое)'
            AiModelDescription_General = 'Подходит для повседневных фото и скриншотов, давая стабильный результат на большинстве неиллюстративных изображений.'
            AiModelDescription_Anime = 'Лучше всего подходит для аниме, комиксов, иллюстраций и чистого линейного рисунка.'
            AiModelDescription_Lightweight = 'Требует меньше ресурсов и работает быстрее, удобно для старых ПК и быстрых задач.'
            AiModelDescription_UpscaylStandard = 'Рекомендуемая модель по умолчанию для большинства изображений, сбалансированная по резкости, деталям и текстурам.'
            AiModelDescription_UpscaylLite = 'Подходит, когда важна скорость, сохраняя аккуратный общий результат за меньшее время.'
            AiModelDescription_UpscaylHighFidelity = 'Лучше сохраняет мелкие детали и текстуры, подходит для детализированных фото и крупных планов.'
            AiModelDescription_UpscaylDigitalArt = 'Подходит для постеров, концепт-арта, рисунков и стилизованных иллюстраций.'
            AiModelDescription_UpscaylRemacri = 'Сильнее восстанавливает детали на естественных фото. Только для некоммерческого использования.'
            AiModelDescription_UpscaylUltramix = 'Балансирует детали и резкость на естественных фото. Только для некоммерческого использования.'
            AiModelDescription_UpscaylUltrasharp = 'Самый резкий вариант для естественных фото. Только для некоммерческого использования.'
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
            AiEnhancementTab = 'أدوات AI'
            AiEnhancementPanelTitle = 'تحسين بالذكاء الاصطناعي'
            AiEnhancementDescription = 'عند التفعيل، تُحسَّن الصور المدعومة أولاً بالذكاء الاصطناعي ثم تُمرَّر إلى مسار التحويل الحالي.'
            AiScale = 'نسبة التكبير'
            AiModel = 'النموذج'
            AiExecutionMode = 'وضع GPU'
            AiExecutionHint = 'يحاول الوضع التلقائي استخدام Vulkan أولاً، ثم يعود إلى وضع CPU إذا كانت الحزمة المضمنة تدعم ذلك.'
            AiInputSupportHint = 'يمكن تحسين صور PNG وJPG وJPEG وWEBP وBMP وTIFF وGIF الثابتة ذات الإطار الواحد. أما ملفات PDF وPSD وSVG وGIF المتحركة وغيرها فتستمر عبر المسار الأصلي.'
            AiModelSeries_RealEsrgan = 'تحسين أساسي'
            AiModelSeries_Upscayl = 'مهيأ حسب المشهد'
            AiModelSelectedSeriesTemplate = 'السلسلة: {0}'
            AiModelSelectedRestriction_NonCommercial = '⚠ للاستخدام غير التجاري فقط'
            AiModel_General = 'صورة يومية'
            AiModel_Anime = 'أنمي ورسوم'
            AiModel_Lightweight = 'سريع وخفيف'
            AiModel_UpscaylStandard = 'تحسين متوازن (موصى به)'
            AiModel_UpscaylLite = 'تحسين سريع'
            AiModel_UpscaylHighFidelity = 'الحفاظ على التفاصيل'
            AiModel_UpscaylDigitalArt = 'فن ورسوم'
            AiModel_UpscaylRemacri = 'تفاصيل الصور الطبيعية (غير تجاري)'
            AiModel_UpscaylUltramix = 'صور طبيعية متوازنة (غير تجاري)'
            AiModel_UpscaylUltrasharp = 'حدة إضافية (غير تجاري)'
            AiModelDescription_General = 'مناسب للصور اليومية ولقطات الشاشة، ويعطي نتيجة مستقرة لمعظم الصور غير الرسومية.'
            AiModelDescription_Anime = 'مناسب لصور الأنمي والقصص المصورة والرسومات التوضيحية والخطوط الواضحة.'
            AiModelDescription_Lightweight = 'يستهلك موارد أقل وينتهي أسرع، وهو مناسب للأجهزة الأقدم أو للمهام السريعة.'
            AiModelDescription_UpscaylStandard = 'الخيار الافتراضي الموصى به لمعظم الصور، مع توازن جيد بين الحدة والتفاصيل والملمس.'
            AiModelDescription_UpscaylLite = 'مناسب عندما تكون السرعة أهم، مع الحفاظ على نتيجة نظيفة في وقت أقصر.'
            AiModelDescription_UpscaylHighFidelity = 'يحافظ على التفاصيل الدقيقة والملمس بشكل أفضل، وهو مناسب للصور الغنية بالتفاصيل واللقطات القريبة.'
            AiModelDescription_UpscaylDigitalArt = 'مناسب للملصقات والفن المفاهيمي والرسومات والأعمال الفنية ذات الطابع الأسلوبي.'
            AiModelDescription_UpscaylRemacri = 'يعزز استعادة التفاصيل في الصور الطبيعية بشكل أقوى. للاستخدام غير التجاري فقط.'
            AiModelDescription_UpscaylUltramix = 'يوازن بين التفاصيل والحدة في الصور الطبيعية. للاستخدام غير التجاري فقط.'
            AiModelDescription_UpscaylUltrasharp = 'الخيار الأكثر حدة للصور الطبيعية. للاستخدام غير التجاري فقط.'
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
