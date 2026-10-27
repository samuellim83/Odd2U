$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$outDir = Join-Path $PSScriptRoot '..\output\ai-benchmark'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromMinutes(5)
$results = @()
$samples = [System.Collections.Generic.List[object]]::new()
$system = 'You are Odd, a calm, practical cat assistant. Be concise and helpful, with occasional subtle cat-like touches. Admit uncertainty. You cannot see the desktop, access files, or perform computer actions. Respond in the user language. Keep replies under 80 words.'
$prompts = @('Introduce yourself in two short sentences.', 'I have 30 minutes to tidy my desk and plan tomorrow. Give me three practical steps.', 'What is 17 multiplied by 6? Answer briefly.', 'Can you see which programs are open on my desktop?')
try {
  # Explicit unload makes the first request a cold model load, not a disk-cache benchmark.
  Invoke-RestMethod http://127.0.0.1:11434/api/generate -Method Post -ContentType application/json -Body '{"model":"qwen3:1.7b","keep_alive":0}' | Out-Null
  foreach ($prompt in $prompts) {
    $body = @{model='qwen3:1.7b';stream=$true;think=$false;keep_alive='5m';options=@{num_ctx=2048;num_predict=192;temperature=0.3;seed=42};messages=@(@{role='system';content=$system},@{role='user';content=$prompt})} | ConvertTo-Json -Depth 8
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post,'http://127.0.0.1:11434/api/chat')
    $request.Content = [System.Net.Http.StringContent]::new($body,[Text.Encoding]::UTF8,'application/json')
    $response = $client.SendAsync($request,[System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    $response.EnsureSuccessStatusCode() | Out-Null
    $reader = [IO.StreamReader]::new($response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
    $reply=''; $first=$null; $final=$null; $thinking=0; $lastSample=-1
    try {
      while ($true) {
        $pending=$reader.ReadLineAsync()
        while (-not $pending.IsCompleted) {
          if ($watch.Elapsed.TotalSeconds -gt 180) {throw 'Generation exceeded 180 seconds'}
          if ([int]$watch.Elapsed.TotalSeconds -ne $lastSample) {
            $lastSample=[int]$watch.Elapsed.TotalSeconds
            $pet=@(Get-Process Odd2U -ErrorAction SilentlyContinue)
            $ollama=@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -match 'ollama|llama'})
            $samples.Add([pscustomobject]@{Seconds=$watch.Elapsed.TotalSeconds;PetCount=$pet.Count;PetResponding=@($pet.Responding);PetWorkingSetMB=($pet|Measure-Object WorkingSet64 -Sum).Sum/1MB;OllamaWorkingSetMB=($ollama|Measure-Object WorkingSet64 -Sum).Sum/1MB})
          }
          Start-Sleep -Milliseconds 50
        }
        $line=$pending.GetAwaiter().GetResult()
        if ($null -eq $line) {break}
        if (-not $line) {continue}
        $item=$line | ConvertFrom-Json
        if ($item.error) {throw $item.error}
        if ($item.message.content) {if ($null -eq $first) {$first=$watch.Elapsed.TotalSeconds}; $reply+=$item.message.content}
        if ($item.message.thinking) {$thinking+=$item.message.thinking.Length}
        if ($item.done) {$final=$item;break}
      }
      if (-not $final) {throw 'Stream ended without completion'}
      $result=[pscustomobject]@{Prompt=$prompt;Reply=$reply;FirstTextSeconds=[math]::Round($first,2);TotalSeconds=[math]::Round($watch.Elapsed.TotalSeconds,2);LoadSeconds=[math]::Round($final.load_duration/1e9,2);TokensPerSecond=[math]::Round($final.eval_count/($final.eval_duration/1e9),2);Tokens=$final.eval_count;DoneReason=$final.done_reason;ThinkingCharacters=$thinking}
      $results+=$result
      $results | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $outDir 'results.json') -Encoding UTF8
      $result | ConvertTo-Json -Compress
    } finally {$reader.Dispose();$response.Dispose();$request.Dispose()}
  }
  Invoke-RestMethod http://127.0.0.1:11434/api/ps | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $outDir 'loaded-model.json') -Encoding UTF8
} finally {
  $samples | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outDir 'process-samples.json') -Encoding UTF8
  $client.Dispose()
}
