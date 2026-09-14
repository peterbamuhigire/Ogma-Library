param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$objects=@(
 '<< /Type /Catalog /Pages 2 0 R >>',
 '<< /Type /Pages /Kids [3 0 R 5 0 R 7 0 R] /Count 3 >>'
)
foreach($pageNumber in 1..3) {
 $contentId=2*$pageNumber+2
 $objects += "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 9 0 R >> >> /Contents $contentId 0 R >>"
 $content="BT /F1 24 Tf 48 710 Td (Ogma synthetic audit fixture - page $pageNumber) Tj 0 -48 Td /F1 16 Tf (Known search phrase: amber library lantern) Tj 0 -36 Td (Original test content. No private books or student data.) Tj ET"
 $objects += "<< /Length $($content.Length) >>`nstream`n$content`nendstream"
}
$objects += '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>'
$pdf=[Text.StringBuilder]::new()
[void]$pdf.Append("%PDF-1.4`n")
$offsets=@(0)
for($i=0;$i -lt $objects.Count;$i++) {
 $offsets += $pdf.Length
 [void]$pdf.Append("$($i+1) 0 obj`n$($objects[$i])`nendobj`n")
}
$xref=$pdf.Length
[void]$pdf.Append("xref`n0 $($objects.Count+1)`n0000000000 65535 f `n")
foreach($offset in $offsets | Select-Object -Skip 1) { [void]$pdf.Append(('{0:D10} 00000 n ' -f $offset)+"`n") }
[void]$pdf.Append("trailer`n<< /Size $($objects.Count+1) /Root 1 0 R >>`nstartxref`n$xref`n%%EOF`n")
[IO.File]::WriteAllBytes($OutputPath,[Text.Encoding]::ASCII.GetBytes($pdf.ToString()))
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
