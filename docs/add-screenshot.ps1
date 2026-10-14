#!/usr/bin/env pwsh
# Adds a screenshot to the repository safely
# Asks you to confirm sensitive data was removed before committing

param(
	[string]$ImagePath,
	[string]$OutputName = $null
)

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Screenshot Security & Upload Tool                     ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

if (-not $ImagePath -or -not (Test-Path $ImagePath)) {
	Write-Host "`n❌ Usage: ./add-screenshot.ps1 -ImagePath 'path/image.png' -OutputName 'dashboard-products'" -ForegroundColor Red
	Write-Host "`nExamples of names in use:" -ForegroundColor Yellow
	Write-Host "  - dashboard-products.png    (Products, the catalogue)"
	Write-Host "  - dashboard-watching.png    (Watching)"
	Write-Host "  - dashboard-stores.png      (Stores)"
	Write-Host "  - dashboard-alerts.png      (Alerts)"
	Write-Host "  - dashboard-settings.png    (Settings)"
	exit 1
}

Write-Host "`n⚠️  SECURITY CHECKLIST:" -ForegroundColor Yellow
Write-Host "  ☐ Remove/blur emails"
Write-Host "  ☐ Remove/blur phone numbers"
Write-Host "  ☐ Remove/blur local IPs"
Write-Host "  ☐ Remove/blur URLs with ports (e.g. localhost:5080)"
Write-Host "  ☐ Remove/blur user names"
Write-Host "  ☐ Remove/blur credentials/tokens"
Write-Host "  ☐ Remove/blur personal data"

$response = Read-Host "`n✅ Did you remove ALL sensitive information? (yes/no)"
if ($response -ne "yes") {
	Write-Host "❌ Cancelled. Come back when the image is ready!" -ForegroundColor Red
	exit 1
}

$outputPath = if ($OutputName) {
	"docs/screenshots/$OutputName.png"
} else {
	$filename = Split-Path -Leaf $ImagePath
	"docs/screenshots/$filename"
}

Write-Host "`n📁 Copying the image..." -ForegroundColor Cyan
Copy-Item -Path $ImagePath -Destination $outputPath -Force
Write-Host "✅ Image copied to: $outputPath" -ForegroundColor Green

Write-Host "`n📝 Adding to git..." -ForegroundColor Cyan
git add $outputPath
git commit -m "docs: Add screenshot - $(Split-Path -Leaf $outputPath)"

Write-Host "`n🚀 Pushing..." -ForegroundColor Cyan
git push origin main

Write-Host "`n✅ Screenshot added!" -ForegroundColor Green
Write-Host "📖 See it on GitHub: https://github.com/rodrigocoelho2/Holo/tree/main/docs/screenshots" -ForegroundColor Green
