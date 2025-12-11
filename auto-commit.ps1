# Автоматический коммит всех изменений
# Использование: .\auto-commit.ps1

$workspaceRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка: не найден git репозиторий" -ForegroundColor Red
    exit 1
}

Set-Location $workspaceRoot

# Проверяем, есть ли изменения для коммита
$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "Нет изменений для коммита" -ForegroundColor Yellow
    exit 0
}

Write-Host "[Git Auto-Commit] Обнаружены изменения, создаю коммит..." -ForegroundColor Yellow

# Добавляем все изменения (включая удалённые файлы)
git add -A

# Получаем список изменённых файлов для сообщения коммита
$changedFiles = git diff --cached --name-status | ForEach-Object {
    $line = $_.Trim()
    if ($line -match '^([AMD])\s+(.+)$') {
        $action = $matches[1]
        $file = $matches[2]
        switch ($action) {
            'A' { "Added: $file" }
            'M' { "Modified: $file" }
            'D' { "Deleted: $file" }
            default { $file }
        }
    }
}

# Создаём сообщение коммита на основе изменений
$commitMessage = "Auto-commit: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
$commitMessage += "`n`nИзменения:`n" + ($changedFiles -join "`n")

# Создаём коммит
git commit -m $commitMessage

if ($LASTEXITCODE -eq 0) {
    Write-Host "[Git Auto-Commit] Коммит создан успешно" -ForegroundColor Green
    Write-Host "[Git Auto-Commit] Post-commit hook автоматически отправит изменения на GitHub" -ForegroundColor Cyan
} else {
    Write-Host "[Git Auto-Commit] Ошибка при создании коммита" -ForegroundColor Red
    exit 1
}

exit 0

