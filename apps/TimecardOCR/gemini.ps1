param(
    [Parameter(Mandatory=$true)][string]$ImageBase64,
    [Parameter(Mandatory=$true)][string]$ApiKey,
    [Parameter(Mandatory=$true)][string]$Model,
    [Parameter(Mandatory=$true)][string]$Side
)
$ErrorActionPreference = 'Stop'
# Called only after explicit user consent. Do not persist or log the API key.
if ($Model -notin @('gemini-3.5-flash-lite','gemini-3.6-flash')) { throw '不支援此 Gemini 模型。' }
if ($Side -notin @('front','back')) { throw '打卡卡面錯誤。' }
if ($ApiKey -notmatch '^[a-zA-Z0-9_-]{15,200}$') { throw 'API Key 格式不正確。' }
if ($ImageBase64.Length -gt 6500000 -or $ImageBase64.Length -lt 200) { throw 'AI 裁切照片大小不正確。' }
try { $imageBytes=[Convert]::FromBase64String($ImageBase64) } catch { throw 'AI 圖片無法解碼。' }
if ($imageBytes.Length -gt 5000000) { throw 'AI 裁切照片不可超過 5 MB。' }
$start=if ($Side -eq 'front') {1} else {16}
$last=if ($Side -eq 'front') {15} else {31}
$prompt=@"
Transcribe a Taiwan timeclock card photographed in a grid-only crop. This is the $Side side of the card. The consecutive day rows are $start through $last; use the printed day number at left to associate time with its row.
Only transcribe VISIBLE dot-matrix clock stamps in the time cells (ignore dates, days, column labels, scribbles, headings, and other numbers). Output 24-hour HH:mm time strings. Do not invent, round, normalize, or guess missing stamps, and do not fabricate pairs. A row can have 1, 2, or 4 time stamps. If unsure about a read time, mark uncertain=true and explain in note. If illegible, omit the value. Do not compute work hours or span dates. Pay attention to faint ink and each horizontal row alignment. One object per nonempty day, each day only once.
Return exactly JSON: {"rows":[{"day":7,"punches":["13:30","15:05"],"uncertain":false,"note":""}]}. For no visible records, return {"rows":[]}. Output JSON ONLY.
"@
$body=@{
  contents=@(@{role='user';parts=@(@{text=$prompt},@{inline_data=@{mime_type='image/jpeg';data=$ImageBase64}})})
  generationConfig=@{responseMimeType='application/json';temperature=0;maxOutputTokens=2300}
} | ConvertTo-Json -Compress -Depth 20
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$url="https://generativelanguage.googleapis.com/v1beta/models/${Model}:generateContent"
try {
    $answer=Invoke-RestMethod -Method Post -Uri $url -Headers @{'x-goog-api-key'=$ApiKey} -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 75
}
catch {
    $code=0
    if ($_.Exception.Response) { try {$code=[int]$_.Exception.Response.StatusCode} catch {} }
    if ($code -eq 400) {throw 'Gemini 請求錯誤（HTTP 400）；可切換模型再試。'}
    elseif ($code -eq 401 -or $code -eq 403) {throw 'Gemini 權限被拒（401/403），請檢查 API Key 或模型權限。'}
    elseif ($code -eq 404) {throw 'Gemini 模型不可用（404），請切換模型。'}
    elseif ($code -eq 429) {throw 'Gemini 已達到使用額度／速率限制（429），請稍後重試。'}
    elseif ($code -ge 500) {throw 'Google 服務暫時無法使用，請稍後重試。'}
    else {throw '無法連線至 Google Gemini。請確認網路、防火牆及 API Key。'}
}
$first=$answer.candidates | Select-Object -First 1
$txt=($first.content.parts | ForEach-Object { $_.text }) -join ''
if ([string]::IsNullOrWhiteSpace($txt)) { throw 'Gemini 沒有回傳可解析的辨識結果。' }
try { $decoded=$txt | ConvertFrom-Json } catch { throw 'Gemini 回傳不是合法 JSON，請重試。' }
if (-not ($decoded.PSObject.Properties.Name -contains 'rows')) { throw 'Gemini 沒有回傳日期陣列，請重試。' }
$clean=New-Object System.Collections.ArrayList
$seen=@{}
foreach ($r in @($decoded.rows)) {
    if ($null -eq $r) { continue }
    $d=0; if (-not [int]::TryParse([string]$r.day,[ref]$d)) {continue}
    if ($d -lt $start -or $d -gt $last -or $seen.ContainsKey($d)) {continue}
    $seen[$d]=$true
    $times=New-Object System.Collections.ArrayList
    foreach ($time in @($r.punches)) {
        if ($null -eq $time) {continue}
        $s=[string]$time
        if ($s -match '^(?:[01]?\d|2[0-3]):[0-5]\d$') { [void]$times.Add($s) }
    }
    if ($times.Count -eq 0) { continue }
    $note=[string]$r.note
    if ($note.Length -gt 160) {$note=$note.Substring(0,160)}
    [void]$clean.Add([pscustomobject]@{day=$d;punches=@($times.ToArray());uncertain=($r.uncertain -ne $false);note=$note})
}
return [pscustomobject]@{ok=$true;rows=@($clean.ToArray());model=$Model}
