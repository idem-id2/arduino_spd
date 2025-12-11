# Автоматический коммит при сохранении файла (для VS Code/Cursor RunOnSave)
# Параметр: путь к сохранённому файлу (опционально)

param(
    [string]$SavedFile = ""
)

$workspaceRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    exit 0
}

Set-Location $workspaceRoot

# Проверяем, есть ли изменения для коммита
$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    exit 0
}

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
if (![string]::IsNullOrWhiteSpace($SavedFile)) {
    $fileName = Split-Path -Leaf $SavedFile
    $commitMessage += "`nSaved: $fileName"
}
$commitMessage += "`n`nИзменения:`n" + ($changedFiles -join "`n")

# Создаём коммит (тихо, без вывода)
git commit -m $commitMessage 2>&1 | Out-Null

# Post-commit hook автоматически отправит изменения на GitHub
exit 0
