param([string]$ReferenceRoot = 'C:/wamp64/www/Ogma-Library/docs/references')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Get-ChildItem -LiteralPath $ReferenceRoot -Filter '*_refreshed_2026-09-10.docx' | Sort-Object Name | ForEach-Object {
    $archive = [IO.Compression.ZipFile]::OpenRead($_.FullName)
    try {
        $reader = [IO.StreamReader]::new($archive.GetEntry('word/document.xml').Open())
        try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
        $ns.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')
        Write-Output ('DOCUMENT: ' + $_.Name)
        Write-Output ('SHA256: ' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)
        $i = 0
        foreach ($paragraph in $document.SelectNodes('//w:body//w:p', $ns)) {
            $i++
            $text = ($paragraph.SelectNodes('.//w:t', $ns) | ForEach-Object { $_.InnerText }) -join ''
            if ($text.Trim()) { Write-Output ('P{0:D4}: {1}' -f $i, $text) }
        }
    } finally { $archive.Dispose() }
}
