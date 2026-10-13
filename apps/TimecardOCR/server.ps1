$ErrorActionPreference = 'Stop'
$Port = 18763
$Root = $PSScriptRoot
$base = "http://127.0.0.1:$Port/"

function Send-Http {
    param([System.IO.Stream]$Stream, [int]$Code, [string]$Type, [string]$Body)
    $data = [Text.Encoding]::UTF8.GetBytes($Body)
    $status = if ($Code -eq 200) {'OK'} elseif ($Code -eq 404) {'Not Found'} else {'Bad Request'}
    $header = "HTTP/1.1 $Code $status`r`nContent-Type: $Type`r`nContent-Length: $($data.Length)`r`nCache-Control: no-store`r`nConnection: close`r`nAccess-Control-Allow-Origin: http://127.0.0.1:$Port`r`n`r`n"
    $head = [Text.Encoding]::ASCII.GetBytes($header)
    $Stream.Write($head,0,$head.Length)
    $Stream.Write($data,0,$data.Length)
    $Stream.Flush()
}

$listener = New-Object System.Net.Sockets.TcpListener([Net.IPAddress]::Loopback,$Port)
try { $listener.Start() } catch { Write-Host "Port $Port is already being used. If the app is open, use your existing browser tab."; Start-Process $base; exit 0 }
Write-Host ''
Write-Host 'Timecard OCR + Excel Portable App' -ForegroundColor Cyan
Write-Host "Running on $base"
Write-Host 'Close this window to stop the local service.'
Write-Host 'Local OCR remains offline. Gemini uses Google only if you explicitly select AI recognition.'
Start-Process $base
try {
    while ($true) {
        $client=$listener.AcceptTcpClient()
        $client.ReceiveTimeout=90000
        $client.SendTimeout=90000
        try {
            $stream=$client.GetStream()
            $reader=New-Object System.IO.StreamReader($stream, [Text.Encoding]::UTF8, $false, 8192, $true)
            $request=$reader.ReadLine()
            if (-not $request) {continue}
            $req=$request.Split(' ')
            if ($req.Length -lt 2) {continue}
            $method=$req[0]; $path=$req[1].Split('?')[0]
            $length=0
            $requestHost=''
            while ($true) {
                $line=$reader.ReadLine()
                if ($null -eq $line -or $line.Length -eq 0) {break}
                if ($line -match '^Content-Length:\s*(\d+)') { $length=[int]$Matches[1] }
                if ($line -match '^Host:\s*(.+)$') { $requestHost=$Matches[1].Trim() }
            }
            if ($requestHost -notin @('127.0.0.1:18763','localhost:18763')) { Send-Http $stream 400 'text/plain; charset=utf-8' 'Invalid local host'; continue }
            if ($method -eq 'GET' -and ($path -eq '/' -or $path -eq '/index.html')) {
                $body=[IO.File]::ReadAllText((Join-Path $Root 'index.html'),[Text.Encoding]::UTF8)
                Send-Http $stream 200 'text/html; charset=utf-8' $body
            }
            elseif ($method -eq 'GET' -and $path -eq '/app.js') {
                $body=[IO.File]::ReadAllText((Join-Path $Root 'app.js'),[Text.Encoding]::UTF8)
                Send-Http $stream 200 'application/javascript; charset=utf-8' $body
            }
            elseif ($method -eq 'GET' -and $path -eq '/ai.js') {
                $body=[IO.File]::ReadAllText((Join-Path $Root 'ai.js'),[Text.Encoding]::UTF8)
                Send-Http $stream 200 'application/javascript; charset=utf-8' $body
            }
            elseif ($method -eq 'POST' -and $path -eq '/api/ocr') {
                if ($length -lt 1 -or $length -gt 18000000) {
                    Send-Http $stream 400 'application/json; charset=utf-8' '{"ok":false,"error":"Image is empty or too large (max 13 MB)."}'
                    continue
                }
                $chars=New-Object char[] $length
                $read=0
                while ($read -lt $length) {
                    $got=$reader.Read($chars,$read,$length-$read)
                    if ($got -le 0) {throw 'Incomplete HTTP request'}
                    $read+=$got
                }
                $payload= -join $chars
                $obj=$payload | ConvertFrom-Json
                if (-not $obj.image) {throw 'Missing image data'}
                $data=[Convert]::FromBase64String($obj.image)
                if ($data.Length -gt 14000000) {throw '圖片太大，請縮小照片後重新上傳。'}
                $tmp=Join-Path ([IO.Path]::GetTempPath()) ('timecard_ocr_'+[guid]::NewGuid().ToString('N')+'.png')
                try {
                    [IO.File]::WriteAllBytes($tmp,$data)
                    $ocrResult=& (Join-Path $Root 'ocr.ps1') -ImagePath $tmp
                    $json=ConvertTo-Json -InputObject $ocrResult -Compress -Depth 8
                    Send-Http $stream 200 'application/json; charset=utf-8' $json
                }
                catch {
                    $msg=$_.Exception.Message
                    $json=ConvertTo-Json -InputObject @{ok=$false;error=$msg} -Compress
                    Send-Http $stream 400 'application/json; charset=utf-8' $json
                }
                finally {Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue}
            }
            elseif ($method -eq 'POST' -and $path -eq '/api/ai') {
                if ($length -lt 1 -or $length -gt 8000000) {
                    Send-Http $stream 400 'application/json; charset=utf-8' '{"ok":false,"error":"AI image too large."}'
                    continue
                }
                $chars=New-Object char[] $length
                $read=0
                while ($read -lt $length) {
                    $got=$reader.Read($chars,$read,$length-$read)
                    if ($got -le 0) { throw 'Incomplete AI request' }
                    $read+=$got
                }
                $obj=(-join $chars) | ConvertFrom-Json
                if (-not $obj.image -or -not $obj.apiKey -or -not $obj.side -or -not $obj.model) {throw 'AI request missing required fields'}
                if ($obj.side -notin @('front','back') -or $obj.model -notin @('gemini-3.5-flash-lite','gemini-3.6-flash')) {throw 'Invalid AI model or side'}
                $result=& (Join-Path $Root 'gemini.ps1') -ImageBase64 ([string]$obj.image) -ApiKey ([string]$obj.apiKey) -Model ([string]$obj.model) -Side ([string]$obj.side)
                $responseJson=ConvertTo-Json -InputObject $result -Compress -Depth 15
                Send-Http $stream 200 'application/json; charset=utf-8' $responseJson
            }
            else { Send-Http $stream 404 'text/plain; charset=utf-8' 'Not found' }
        }
        catch {
            try {
                $json=ConvertTo-Json -InputObject @{ok=$false;error=$_.Exception.Message} -Compress
                Send-Http $stream 400 'application/json; charset=utf-8' $json
            }catch{}
            Write-Host "Request error: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        finally {if ($null -ne $reader) {$reader.Dispose()};$client.Close();$reader=$null}
    }
}
finally {$listener.Stop()}
