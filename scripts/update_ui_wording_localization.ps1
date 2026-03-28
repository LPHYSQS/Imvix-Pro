using namespace System.Collections.Generic
using namespace System.IO
using namespace System.Text

$translations = [ordered]@{
    'en-US' = [ordered]@{
        AiEnhancementToggleHint = 'Enable AI enhancement preprocessing before the original conversion flow.'
        ImportImages = 'Import Files'
        ImageList = 'File List'
        DropHintTitle = 'Drop files here'
        DropHintDescription = "or click ""Import Files"" above`n(Supported: PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF, PSD, EXE, desktop shortcuts)"
        NoPreview = 'Select a file to preview'
        ImportDialogTitle = 'Select files to import'
        ImportSupportedFiles = 'Supported Files'
    }
    'zh-CN' = [ordered]@{
        AiEnhancementToggleHint = '开启后，会在原有转换流程前先执行 AI 增强预处理。'
        ImportImages = '导入文件'
        ImageList = '文件列表'
        DropHintTitle = '拖拽文件到这里'
        DropHintDescription = "或点击上方""导入文件""按钮`n（支持：PNG、JPG、JPEG、WEBP、BMP、GIF、TIFF、ICO、SVG、PDF、PSD、EXE、桌面快捷方式）"
        NoPreview = '请选择一个文件预览'
        ImportDialogTitle = '选择要导入的文件'
        ImportSupportedFiles = '支持的文件'
    }
    'zh-TW' = [ordered]@{
        AiEnhancementToggleHint = '開啟後，會在原有轉換流程前先執行 AI 增強前處理。'
        ImportImages = '匯入檔案'
        ImageList = '檔案清單'
        DropHintTitle = '拖曳檔案到這裡'
        DropHintDescription = "或點擊上方""匯入檔案""按鈕`n（支援：PNG、JPG、JPEG、WEBP、BMP、GIF、TIFF、ICO、SVG、PDF、PSD、EXE、桌面捷徑）"
        NoPreview = '請選擇一個檔案預覽'
        ImportDialogTitle = '選擇要匯入的檔案'
        ImportSupportedFiles = '支援的檔案'
    }
    'ja-JP' = [ordered]@{
        AiEnhancementToggleHint = '有効にすると、既存の変換フローの前に AI 強化の前処理を実行します。'
        ImportImages = 'ファイルをインポート'
        ImageList = 'ファイル一覧'
        DropHintTitle = 'ファイルをここにドラッグ'
        DropHintDescription = "または上部の「ファイルをインポート」ボタンをクリック`n（対応形式: PNG、JPG、JPEG、WEBP、BMP、GIF、TIFF、ICO、SVG、PDF、PSD、EXE、デスクトップショートカット）"
        NoPreview = 'プレビューするファイルを選択してください'
        ImportDialogTitle = 'インポートするファイルを選択'
        ImportSupportedFiles = '対応ファイル'
    }
    'ko-KR' = [ordered]@{
        AiEnhancementToggleHint = '사용하면 기존 변환 흐름 전에 AI 향상 전처리를 실행합니다.'
        ImportImages = '파일 가져오기'
        ImageList = '파일 목록'
        DropHintTitle = '파일을 여기로 끌어오세요'
        DropHintDescription = "또는 위의 ""파일 가져오기"" 버튼을 클릭하세요`n(지원 형식: PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF, PSD, EXE, 바탕 화면 바로 가기)"
        NoPreview = '미리 볼 파일을 선택하세요'
        ImportDialogTitle = '가져올 파일 선택'
        ImportSupportedFiles = '지원 파일'
    }
    'de-DE' = [ordered]@{
        AiEnhancementToggleHint = 'Aktiviert die KI-Vorverarbeitung vor dem bisherigen Konvertierungsablauf.'
        ImportImages = 'Dateien importieren'
        ImageList = 'Dateiliste'
        DropHintTitle = 'Dateien hierher ziehen'
        DropHintDescription = "oder oben auf ""Dateien importieren"" klicken`n(Unterstützt: PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF, PSD, EXE, Desktopverknüpfungen)"
        NoPreview = 'Wählen Sie eine Datei zur Vorschau aus'
        ImportDialogTitle = 'Dateien zum Import auswählen'
        ImportSupportedFiles = 'Unterstützte Dateien'
    }
    'fr-FR' = [ordered]@{
        AiEnhancementToggleHint = 'Active le prétraitement d''amélioration IA avant le flux de conversion existant.'
        ImportImages = 'Importer des fichiers'
        ImageList = 'Liste des fichiers'
        DropHintTitle = 'Déposez des fichiers ici'
        DropHintDescription = "ou cliquez sur ""Importer des fichiers"" ci-dessus`n(Pris en charge : PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF, PSD, EXE, raccourcis du bureau)"
        NoPreview = 'Sélectionnez un fichier à prévisualiser'
        ImportDialogTitle = 'Sélectionner les fichiers à importer'
        ImportSupportedFiles = 'Fichiers pris en charge'
    }
    'it-IT' = [ordered]@{
        AiEnhancementToggleHint = 'Attiva il pre-processo di miglioramento AI prima del flusso di conversione esistente.'
        ImportImages = 'Importa file'
        ImageList = 'Elenco file'
        DropHintTitle = 'Trascina qui i file'
        DropHintDescription = "oppure fai clic su ""Importa file"" qui sopra`n(Supportati: PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF, PSD, EXE, collegamenti desktop)"
        NoPreview = 'Seleziona un file da visualizzare in anteprima'
        ImportDialogTitle = 'Seleziona i file da importare'
        ImportSupportedFiles = 'File supportati'
    }
    'ru-RU' = [ordered]@{
        AiEnhancementToggleHint = 'Включает предварительную AI-обработку перед существующим процессом конвертации.'
        ImportImages = 'Импорт файлов'
        ImageList = 'Список файлов'
        DropHintTitle = 'Перетащите файлы сюда'
        DropHintDescription = "или нажмите кнопку ""Импорт файлов"" выше`n(Поддерживается: PNG, JPG, JPEG, WEBP, BMP, GIF, TIFF, ICO, SVG, PDF, PSD, EXE, ярлыки рабочего стола)"
        NoPreview = 'Выберите файл для предварительного просмотра'
        ImportDialogTitle = 'Выберите файлы для импорта'
        ImportSupportedFiles = 'Поддерживаемые файлы'
    }
    'ar-SA' = [ordered]@{
        AiEnhancementToggleHint = 'يُفعّل المعالجة المسبقة للتحسين بالذكاء الاصطناعي قبل مسار التحويل الحالي.'
        ImportImages = 'استيراد الملفات'
        ImageList = 'قائمة الملفات'
        DropHintTitle = 'اسحب الملفات إلى هنا'
        DropHintDescription = "أو انقر فوق زر ""استيراد الملفات"" في الأعلى`n(المدعوم: PNG وJPG وJPEG وWEBP وBMP وGIF وTIFF وICO وSVG وPDF وPSD وEXE واختصارات سطح المكتب)"
        NoPreview = 'حدد ملفًا لمعاينته'
        ImportDialogTitle = 'حدد الملفات المطلوب استيرادها'
        ImportSupportedFiles = 'الملفات المدعومة'
    }
}

$managedKeys = [HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($key in $translations['en-US'].Keys) {
    [void]$managedKeys.Add($key)
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
    $path = Join-Path (Get-Location) "Assets/Localization/$languageCode.json"
    $json = Get-Content -LiteralPath $path -Raw
    $document = $json | ConvertFrom-Json
    $entries = [List[object]]::new()
    $currentKeys = [HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $insertedImportSupportedFiles = $false

    foreach ($property in $document.PSObject.Properties) {
        [void]$currentKeys.Add($property.Name)
    }

    foreach ($property in $document.PSObject.Properties) {
        if ($managedKeys.Contains($property.Name)) {
            Add-Entry -Entries $entries -Key $property.Name -Value ([string]$translations[$languageCode][$property.Name])
        }
        else {
            Add-Entry -Entries $entries -Key $property.Name -Value ([string]$property.Value)
        }

        if ($property.Name -eq 'ImportDialogTitle' -and -not $currentKeys.Contains('ImportSupportedFiles')) {
            Add-Entry -Entries $entries -Key 'ImportSupportedFiles' -Value ([string]$translations[$languageCode]['ImportSupportedFiles'])
            $insertedImportSupportedFiles = $true
        }
    }

    if (-not $currentKeys.Contains('ImportSupportedFiles') -and -not $insertedImportSupportedFiles) {
        Add-Entry -Entries $entries -Key 'ImportSupportedFiles' -Value ([string]$translations[$languageCode]['ImportSupportedFiles'])
    }

    $lines = [List[string]]::new()
    $lines.Add('{') | Out-Null

    for ($index = 0; $index -lt $entries.Count; $index++) {
        $entry = $entries[$index]
        $lines.Add((Format-JsonLine -Key $entry.Key -Value $entry.Value -HasComma ($index -lt ($entries.Count - 1)))) | Out-Null
    }

    $lines.Add('}') | Out-Null
    [File]::WriteAllText($path, ($lines -join "`r`n") + "`r`n", [UTF8Encoding]::new($true))
}
