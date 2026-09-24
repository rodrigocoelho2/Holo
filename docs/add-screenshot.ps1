#!/usr/bin/env pwsh
# Script para adicionar screenshots ao repositório de forma segura
# Remove informações sensíveis antes de commitar

param(
	[string]$ImagePath,
	[string]$OutputName = $null
)

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Screenshot Security & Upload Tool                     ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

if (-not $ImagePath -or -not (Test-Path $ImagePath)) {
	Write-Host "`n❌ Uso: ./add-screenshot.ps1 -ImagePath 'caminho/imagem.png' -OutputName 'dashboard-overview'" -ForegroundColor Red
	Write-Host "`nExemplos de nomes válidos:" -ForegroundColor Yellow
	Write-Host "  - dashboard-overview.png    (Visão geral com estatísticas)"
	Write-Host "  - dashboard-tasks.png       (Lista de tarefas)"
	Write-Host "  - dashboard-stores.png      (Lojas configuradas)"
	Write-Host "  - dashboard-alerts.png      (Histórico de alertas)"
	Write-Host "  - dashboard-settings.png    (Definições)"
	exit 1
}

Write-Host "`n⚠️  CHECKLIST DE SEGURANÇA:" -ForegroundColor Yellow
Write-Host "  ☐ Remover/borrar emails"
Write-Host "  ☐ Remover/borrar números de telefone"
Write-Host "  ☐ Remover/borrar IPs locais"
Write-Host "  ☐ Remover/borrar URLs com portas (ex: localhost:5080)"
Write-Host "  ☐ Remover/borrar nomes de utilizadores"
Write-Host "  ☐ Remover/borrar credenciais/tokens"
Write-Host "  ☐ Remover/borrar dados pessoais"

$response = Read-Host "`n✅ Confirmaste que removeste ALL as informações sensíveis? (sim/não)"
if ($response -ne "sim") {
	Write-Host "❌ Cancelado. Volta quando tiveres a imagem pronta!" -ForegroundColor Red
	exit 1
}

$outputPath = if ($OutputName) {
	"docs/screenshots/$OutputName.png"
} else {
	$filename = Split-Path -Leaf $ImagePath
	"docs/screenshots/$filename"
}

Write-Host "`n📁 Copiando imagem..." -ForegroundColor Cyan
Copy-Item -Path $ImagePath -Destination $outputPath -Force
Write-Host "✅ Imagem copiada para: $outputPath" -ForegroundColor Green

Write-Host "`n📝 Adicionando ao Git..." -ForegroundColor Cyan
git add $outputPath
git commit -m "docs: Add screenshot - $(Split-Path -Leaf $outputPath)"

Write-Host "`n🚀 Fazendo push..." -ForegroundColor Cyan
git push origin main

Write-Host "`n✅ Screenshot adicionado com sucesso!" -ForegroundColor Green
Write-Host "📖 A imagem aparecerá no README em: https://github.com/rodrigocoelho2/tcg-stock-bot" -ForegroundColor Green
