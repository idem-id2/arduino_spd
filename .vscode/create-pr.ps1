# Автоматическое создание Pull Request через GitHub API
# Вызывается после успешного push

param(
    [string]$BranchName,
    [string]$BaseBranch = "main"
)

# Получаем путь к рабочей директории
$workspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location $workspaceRoot

# Проверяем, что мы в git репозитории
if (-not (Test-Path ".git")) {
    Write-Host "Not a git repository, skipping PR creation" -ForegroundColor Yellow
    exit 0
}

# Получаем информацию о репозитории из remote URL
$remoteUrl = git remote get-url origin
$owner = $null
$repo = $null

if ($remoteUrl -match "github\.com[:/]([^/]+)/([^/]+)\.git") {
    $owner = $matches[1]
    $repo = $matches[2] -replace '\.git$', ''
} elseif ($remoteUrl -match "github\.com[:/]([^/]+)/([^/]+)") {
    $owner = $matches[1]
    $repo = $matches[2] -replace '\.git$', ''
}

if ($null -eq $owner -or $null -eq $repo) {
    Write-Host "Could not parse GitHub repository URL: $remoteUrl" -ForegroundColor Yellow
    exit 0
}

# Извлекаем токен из remote URL
$token = $null
if ($remoteUrl -match "ghp_[A-Za-z0-9]{36}") {
    $token = $matches[0]
} elseif ($remoteUrl -match "ghp_[^@]+") {
    $token = $matches[0]
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "GitHub token not found in remote URL" -ForegroundColor Yellow
    exit 0
}

# Если имя ветки не указано, получаем текущую
if ([string]::IsNullOrWhiteSpace($BranchName)) {
    $BranchName = git branch --show-current
    if ([string]::IsNullOrWhiteSpace($BranchName)) {
        Write-Host "Could not determine current branch" -ForegroundColor Yellow
        exit 0
    }
}

# Проверяем, существует ли уже PR для этой ветки
$apiUrl = "https://api.github.com/repos/$owner/$repo/pulls"
$headers = @{
    "Authorization" = "token $token"
    "Accept" = "application/vnd.github.v3+json"
}

try {
    $queryUrl = "$apiUrl" + "?head=$owner`:$BranchName&state=open"
    $existingPRs = Invoke-RestMethod -Uri $queryUrl -Headers $headers -Method Get
    if ($existingPRs.Count -gt 0) {
        $existingPrUrl = $existingPRs[0].html_url
        $msg = "PR already exists for branch " + $BranchName + ": " + $existingPrUrl
        Write-Host $msg -ForegroundColor Gray
        exit 0
    }
} catch {
    $checkErrorMsg = $_.Exception.Message
    $errorMsg = "Error checking existing PRs: " + $checkErrorMsg
    Write-Host $errorMsg -ForegroundColor Yellow
    # Продолжаем создание PR
}

# Получаем последний коммит для описания
$lastCommit = git log -1 --pretty=format:"%s" 2>&1
$lastCommitHash = git log -1 --pretty=format:"%h" 2>&1

# Формируем данные для PR
$prTitle = "Auto-update: " + $BranchName

# Формируем body PR без кириллицы в массиве для избежания проблем с кодировкой
$branchInfo = "**Branch:** " + $BranchName
$commitInfo = "**Last commit:** " + $lastCommitHash + " - " + $lastCommit
$prBodyLines = @(
    "## Automatic update",
    "",
    $branchInfo,
    $commitInfo,
    "",
    "This PR was created automatically when saving files.",
    "",
    "### Changes",
    "- Automatic commit of changes",
    "- Automatic push to branch",
    "- Automatic PR creation",
    "",
    "---",
    "*Created automatically via RunOnSave*"
)
$prBody = $prBodyLines -join "`n"

$prData = @{
    title = $prTitle
    body = $prBody
    head = $BranchName
    base = $BaseBranch
} | ConvertTo-Json

# Создаем PR через GitHub API
try {
    $response = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Post -Body $prData -ContentType "application/json"
    $newPrUrl = $response.html_url
    $successMsg = "PR created: " + $newPrUrl
    Write-Host $successMsg -ForegroundColor Green
    exit 0
} catch {
    $errorMessage = $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        try {
            $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
            $errorMessage = $errorDetails.message
        } catch {
            # Игнорируем ошибки парсинга JSON
        }
    }
    
    # Если PR уже существует - это нормально
    if ($errorMessage -match "already exists" -or $errorMessage -match "pull request already exists") {
        $existsMsg = "PR already exists for branch " + $BranchName
        Write-Host $existsMsg -ForegroundColor Gray
        exit 0
    }
    
    $errMsg = "Error creating PR: " + $errorMessage
    Write-Host $errMsg -ForegroundColor Yellow
    exit 0  # Не прерываем процесс, если PR не создался
}
