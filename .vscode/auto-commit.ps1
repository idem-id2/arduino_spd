# Скрипт для автоматического commit и push
# Запускается при сохранении файлов

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$message = "Auto-save: $timestamp"

# Добавить все изменения
git add -A

# Проверить, есть ли изменения для коммита
$status = git status --porcelain
if ($status) {
    # Создать коммит
    git commit -m $message
    
    # Отправить на GitHub
    git push origin main
    
    Write-Host "✓ Автоматически сохранено и отправлено: $message" -ForegroundColor Green
} else {
    Write-Host "Нет изменений для коммита" -ForegroundColor Gray
}

