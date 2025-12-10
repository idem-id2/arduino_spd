# Автоматический коммит и push при сохранении файла
# Используется расширением RunOnSave

param(
    [string]$FilePath
)

# Получаем путь к рабочей директории
$workspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location $workspaceRoot

# Проверяем, что мы в git репозитории
if (-not (Test-Path ".git")) {
    Write-Host "Not a git repository, skipping auto-commit" -ForegroundColor Yellow
    exit 0
}

# Получаем относительный путь файла от корня репозитория
$relativePath = $FilePath.Replace($workspaceRoot, "").TrimStart("\", "/")
$fileName = Split-Path -Leaf $FilePath

# Игнорируем файлы в bin/, obj/, .vs/ и другие временные файлы
if ($relativePath -match "(\\|/)(bin|obj|Debug|Release|\.vs)(\\|/)") {
    Write-Host "Skipping auto-commit for temporary file: $relativePath" -ForegroundColor Gray
    exit 0
}

# Игнорируем файлы настроек пользователя
if ($fileName -match "\.(user|suo)$") {
    Write-Host "Skipping auto-commit for user settings file: $fileName" -ForegroundColor Gray
    exit 0
}

# Проверяем, есть ли изменения для коммита
$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "No changes to commit for: $fileName" -ForegroundColor Gray
    exit 0
}

# Добавляем только измененный файл (не все файлы)
git add "$relativePath"

# Формируем сообщение коммита с временем
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$shortPath = if ($relativePath.Length -gt 50) { 
    "..." + $relativePath.Substring($relativePath.Length - 47) 
} else { 
    $relativePath 
}
$commitMessage = "Auto-save: $timestamp - $shortPath"

# Создаем коммит
try {
    git commit -m $commitMessage
    
    # Push выполнится автоматически через git hook post-commit
    Write-Host "✓ Auto-committed: $shortPath at $timestamp" -ForegroundColor Green
}
catch {
    # Если коммит не удался (например, нет изменений), это нормально
    if ($_.Exception.Message -match "nothing to commit") {
        Write-Host "No changes to commit for: $fileName" -ForegroundColor Gray
        exit 0
    }
    Write-Host "Error committing: $_" -ForegroundColor Red
    exit 1
}
