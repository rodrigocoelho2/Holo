# 📸 Como Adicionar Screenshots ao Repositório

## ✅ Instruções Rápidas

### Opção 1: Usar o Script PowerShell (Recomendado)

```powershell
cd \\PROMETHEUS\docker\tcg-stock-bot
.\docs\add-screenshot.ps1 -ImagePath "C:\caminho\para\image.png" -OutputName "dashboard-overview"
```

O script irá:
1. ✅ Fazer checklist de segurança
2. ✅ Pedir confirmação de que removeste dados sensíveis
3. ✅ Copiar a imagem para `docs/screenshots/`
4. ✅ Fazer commit automático
5. ✅ Fazer push para GitHub

### Opção 2: Manual

1. **Processa a imagem** (remove dados sensíveis):
   - Abre a screenshot em Paint, GIMP ou https://www.pixlr.com/
   - Borra/retira: emails, números de telefone, IPs, URLs com portas, credenciais
   - Salva como PNG

2. **Copia para a pasta correta**:
   ```powershell
   Copy-Item -Path "C:\caminho\imagem.png" -Destination "docs\screenshots\dashboard-overview.png"
   ```

3. **Faz commit e push**:
   ```powershell
   cd \\PROMETHEUS\docker\tcg-stock-bot
   git add docs/screenshots/dashboard-overview.png
   git commit -m "docs: Add screenshot - dashboard-overview"
   git push origin main
   ```

## 📋 Nomes de ficheiros esperados

| Nome | Conteúdo | Localizado em |
|------|----------|---------------|
| `dashboard-overview.png` | Visão Geral (68 alertas, gráfico 14 dias, estatísticas) | README.md linha ~60 |
| `dashboard-tasks.png` | Tarefas (lista de produtos monitorados) | README.md linha ~75 |
| `dashboard-stores.png` | Lojas (lista de lojas configuradas) | README.md linha ~90 |

## 🔒 Checklist de Segurança

Antes de commitar, verifica se removeste:

- [ ] ✅ Emails (ex: fenomjr@gmail.com)
- [ ] ✅ Números de telefone (ex: +351918739564)
- [ ] ✅ IPs locais (ex: 192.168.x.x, localhost)
- [ ] ✅ URLs com portas (ex: localhost:5080)
- [ ] ✅ Nomes de utilizadores
- [ ] ✅ Credenciais/API keys/Tokens
- [ ] ✅ Dados pessoais
- [ ] ✅ Qualquer informação sensível

## 📖 Resultado

Depois de adicionar as imagens, aparecerão automaticamente no README:
- 🌐 GitHub: https://github.com/rodrigocoelho2/tcg-stock-bot
- 📄 Secção: "📸 Exemplos do Dashboard"

## ⚠️ Se cometes um erro...

Se acidentalmente commites uma screenshot com dados sensíveis:

```powershell
# Remove do último commit (se ainda não fizeste push)
git reset HEAD~1
git reset -- docs/screenshots/problematic-file.png
rm docs/screenshots/problematic-file.png

# Se já fizeste push, segue o procedimento de "git filter-branch"
# que já usamos antes para limpar segredos
```

---

**Precisa de ajuda?** Consulta `docs/screenshots/README.md` para mais detalhes.
