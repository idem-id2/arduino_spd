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

# Нормализуем пути для корректной обработки разных разделителей (/, \)
# Используем встроенные функции PowerShell для работы с путями
$normalizedWorkspaceRoot = [System.IO.Path]::GetFullPath($workspaceRoot)
$normalizedFilePath = [System.IO.Path]::GetFullPath($FilePath)

# Получаем относительный путь файла от корня репозитория
# Используем .NET метод GetRelativePath для надежной обработки путей
try {
    $relativePath = [System.IO.Path]::GetRelativePath($normalizedWorkspaceRoot, $normalizedFilePath)
    # Нормализуем разделители для Git (используем прямые слеши)
    $relativePath = $relativePath.Replace('\', '/')
} catch {
    # Fallback: если GetRelativePath не доступен (старые версии .NET)
    # Используем нормализованные пути с единообразными разделителями
    $normalizedWorkspaceRootForReplace = $normalizedWorkspaceRoot.Replace('\', '/').TrimEnd('/')
    $normalizedFilePathForReplace = $normalizedFilePath.Replace('\', '/')
    
    # Проверяем, что файл действительно находится внутри workspace
    if (-not $normalizedFilePathForReplace.StartsWith($normalizedWorkspaceRootForReplace, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "File is outside workspace root, skipping auto-commit: $FilePath" -ForegroundColor Yellow
        exit 0
    }
    
    # Получаем относительный путь с учетом регистра
    $relativePath = $normalizedFilePathForReplace.Substring($normalizedWorkspaceRootForReplace.Length).TrimStart("/")
}

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
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to add file to git: $relativePath" -ForegroundColor Red
    Write-Host "Git error code: $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# Формируем сообщение коммита с временем
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$shortPath = if ($relativePath.Length -gt 50) { 
    "..." + $relativePath.Substring($relativePath.Length - 47) 
} else { 
    $relativePath 
}
$commitMessage = "Auto-save: $timestamp - $shortPath"

# Создаем коммит
# Внешние команды (git) не выбрасывают исключения в PowerShell,
# поэтому проверяем $LASTEXITCODE после выполнения
$commitOutput = git commit -m $commitMessage 2>&1
$commitExitCode = $LASTEXITCODE
$commitError = $commitOutput | Out-String

if ($commitExitCode -eq 0) {
    # Push выполнится автоматически через git hook post-commit
    Write-Host "✓ Auto-committed: $shortPath at $timestamp" -ForegroundColor Green
    exit 0
}
else {
    # Коммит не удался - проверяем причину
    # Если нет изменений для коммита - это нормально
    if ($commitError -match "nothing to commit" -or $commitError -match "no changes added to commit") {
        Write-Host "No changes to commit for: $fileName" -ForegroundColor Gray
        exit 0
    }
    
    # Другие ошибки - выводим и завершаем с ошибкой
    Write-Host "Error committing: $commitError" -ForegroundColor Red
    Write-Host "Git exit code: $commitExitCode" -ForegroundColor Red
    exit 1
}
