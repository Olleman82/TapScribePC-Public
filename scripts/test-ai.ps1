param(
  [string]$EnvPath = ""
)

function Find-EnvPath {
  param([string]$startDir)
  $current = $startDir
  for ($i = 0; $i -lt 5; $i++) {
    $candidate = Join-Path $current ".env"
    if (Test-Path $candidate) { return $candidate }
    $parent = Split-Path -Parent $current
    if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) { break }
    $current = $parent
  }
  return ""
}

function Load-Env($path) {
  $map = @{}
  if (-not (Test-Path $path)) { return $map }
  Get-Content $path | ForEach-Object {
    $line = $_.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { return }
    $idx = $line.IndexOf('=')
    if ($idx -le 0) { return }
    $key = $line.Substring(0, $idx).Trim()
    $val = $line.Substring($idx + 1).Trim().Trim('"')
    $map[$key] = $val
  }
  return $map
}

$resolvedEnvPath = $EnvPath
if ([string]::IsNullOrWhiteSpace($resolvedEnvPath)) {
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
  $resolvedEnvPath = Find-EnvPath $scriptDir
}
if ([string]::IsNullOrWhiteSpace($resolvedEnvPath)) {
  $resolvedEnvPath = "D:\\Appar\\wspr-pc\\.env"
}

$envMap = Load-Env $resolvedEnvPath
$geminiKey = $envMap['GEMINI_API_KEY']
$openaiKey = $envMap['OPENAI_API_KEY']
$geminiModel = $envMap['GEMINI_MODEL']
$geminiModelList = $envMap['GEMINI_MODEL_LIST']
$openaiModel = $envMap['OPENAI_MODEL']
$openaiModelList = $envMap['OPENAI_MODEL_LIST']
$openaiReasoning = $envMap['OPENAI_REASONING']
$openaiReasoningList = $envMap['OPENAI_REASONING_LIST']
$debugTests = $envMap['DEBUG_AI_TESTS']
if (-not $debugTests) { $debugTests = $env:DEBUG_AI_TESTS }

if (-not $geminiModel) { $geminiModel = "gemini-flash-latest" }
if (-not $openaiModel) { $openaiModel = "gpt-5-mini" }
if (-not $openaiReasoning) { $openaiReasoning = "minimal" }

if (-not $geminiKey) {
  Write-Host "GEMINI_API_KEY missing in .env" -ForegroundColor Yellow
}
if (-not $openaiKey) {
  Write-Host "OPENAI_API_KEY missing in .env" -ForegroundColor Yellow
}

$errors = @()

function Get-ErrorBody($err) {
  try {
    if ($err.ErrorDetails -and $err.ErrorDetails.Message) {
      return $err.ErrorDetails.Message
    }
    $resp = $err.Exception.Response
    if ($resp -is [System.Net.Http.HttpResponseMessage]) {
      return $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    }
    if ($resp -is [System.Net.WebResponse]) {
      $stream = $resp.GetResponseStream()
      if ($stream) {
        $reader = New-Object System.IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
        $reader.Close()
        return $text
      }
    }
  } catch {
    return ""
  }
  return ""
}

function Invoke-GeminiRaw($apiVersion, $model, $prompt, $thinkingBudget, $grounding) {
  $modelPath = if ($model -like "models/*") { $model } else { "models/$model" }
  $uri = "https://generativelanguage.googleapis.com/$apiVersion/${modelPath}:generateContent?key=$geminiKey"
  if ($debugTests -eq "1") {
    $safeUri = $uri -replace [regex]::Escape($geminiKey), "***"
    Write-Host "Gemini URL: $safeUri" -ForegroundColor DarkGray
  }
  $body = @{ contents = @(@{ parts = @(@{ text = $prompt }) }) }
  $body.generationConfig = @{ thinkingConfig = @{ thinkingBudget = $thinkingBudget } }
  if ($grounding) {
    $body.tools = @(@{ google_search = @{} })
  }
  $json = $body | ConvertTo-Json -Depth 6

  $headers = @{ "x-goog-api-key" = $geminiKey }
  Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $json -ContentType "application/json"
}

function Invoke-Gemini($model, $prompt, $thinkingBudget, $grounding) {
  try {
    return Invoke-GeminiRaw "v1beta" $model $prompt $thinkingBudget $grounding
  } catch {
    if ($_.Exception.Message -match "404") {
      return Invoke-GeminiRaw "v1" $model $prompt $thinkingBudget $grounding
    }
    throw
  }
}

function Is-GeminiUnsupported($detail, $needle) {
  if (-not $detail) { return $false }
  return $detail -match $needle
}

function List-GeminiModels() {
  $uri = "https://generativelanguage.googleapis.com/v1beta/models"
  $headers = @{ "x-goog-api-key" = $geminiKey }
  Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
}

function Invoke-OpenAI($model, $system, $prompt, $reasoning) {
  $uri = "https://api.openai.com/v1/responses"
  $input = @()
  if ($system) {
    $input += @{ role = "system"; content = $system }
  }
  $input += @{ role = "user"; content = $prompt }

  $body = @{ model = $model; input = $input; reasoning = @{ effort = $reasoning } }
  $json = $body | ConvertTo-Json -Depth 6
  $headers = @{ Authorization = "Bearer $openaiKey" }
  Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $json -ContentType "application/json"
}

$testPrompt = "Svara kort med ett ord: Hej"

if ($geminiKey) {
  $geminiModels = @()
  if ($geminiModelList) {
    $geminiModels = $geminiModelList.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
  } else {
    try {
      $listResp = List-GeminiModels
      $modelsRaw = @()
      if ($listResp.models) { $modelsRaw = $listResp.models }
      if ($modelsRaw.Count -gt 0) {
        $flash = $modelsRaw | Where-Object { $_.name -match "flash" } | Select-Object -First 3
        $pick = if ($flash.Count -gt 0) { $flash } else { $modelsRaw | Select-Object -First 3 }
        $geminiModels = $pick | ForEach-Object { $_.name }
        Write-Host ("Gemini models from API: " + ($geminiModels -join ", ")) -ForegroundColor DarkGray
      }
    } catch {
      $geminiModels = @($geminiModel)
    }
    if ($geminiModels.Count -eq 0) {
      if ($geminiModel -eq "gemini-flash-latest") {
        $geminiModels = @("gemini-flash-latest", "gemini-2.5-flash", "gemini-1.5-flash")
      } else {
        $geminiModels = @($geminiModel)
      }
    }
  }

  foreach ($model in $geminiModels) {
    try {
      Write-Host "Gemini: model=$model thinking off, grounding off" -ForegroundColor Cyan
      $resp = Invoke-Gemini $model $testPrompt 0 $false
      Write-Host "OK" -ForegroundColor Green
    } catch {
      $detail = Get-ErrorBody $_
      $suffix = if ($detail) { " :: $detail" } else { "" }
      $errors += "Gemini thinking off failed (model=$model): $($_.Exception.Message)$suffix"
    }

    try {
      Write-Host "Gemini: model=$model thinking on, grounding off" -ForegroundColor Cyan
      $resp = Invoke-Gemini $model $testPrompt -1 $false
      Write-Host "OK" -ForegroundColor Green
    } catch {
      $detail = Get-ErrorBody $_
      if (Is-GeminiUnsupported $detail "thinking is not supported") {
        Write-Host "Gemini: model=$model thinking not supported (skipped)" -ForegroundColor Yellow
      } else {
        $suffix = if ($detail) { " :: $detail" } else { "" }
        $errors += "Gemini thinking on failed (model=$model): $($_.Exception.Message)$suffix"
      }
    }

    try {
      Write-Host "Gemini: model=$model thinking off, grounding on" -ForegroundColor Cyan
      $resp = Invoke-Gemini $model $testPrompt 0 $true
      Write-Host "OK" -ForegroundColor Green
    } catch {
      $detail = Get-ErrorBody $_
      if (Is-GeminiUnsupported $detail "Search Grounding is not supported") {
        Write-Host "Gemini: model=$model grounding not supported (skipped)" -ForegroundColor Yellow
      } else {
        $suffix = if ($detail) { " :: $detail" } else { "" }
        $errors += "Gemini grounding failed (model=$model): $($_.Exception.Message)$suffix"
      }
    }
  }
}

if ($openaiKey) {
  $models = @()
  if ($openaiModelList) {
    $models = $openaiModelList.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
  } else {
    $models = @($openaiModel)
  }

  $reasoningList = @()
  if ($openaiReasoningList) {
    $reasoningList = $openaiReasoningList.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
  } else {
    $reasoningList = @($openaiReasoning)
  }

  foreach ($model in $models) {
    foreach ($eff in $reasoningList) {
      try {
        Write-Host "OpenAI: model=$model reasoning=$eff" -ForegroundColor Cyan
        $resp = Invoke-OpenAI $model "Du är en hjälpsam assistent." $testPrompt $eff
        Write-Host "OK" -ForegroundColor Green
      } catch {
        $detail = Get-ErrorBody $_
        $suffix = if ($detail) { " :: $detail" } else { "" }
        $errors += "OpenAI failed (model=$model reasoning=$eff): $($_.Exception.Message)$suffix"
      }
    }
  }
}

if ($errors.Count -gt 0) {
  Write-Host "---" -ForegroundColor Yellow
  $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
  exit 1
}

Write-Host "All tests completed." -ForegroundColor Green
