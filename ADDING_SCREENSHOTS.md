# Adding screenshots to the repository

## Quick instructions

### Option 1: the PowerShell script (recommended)

```powershell
cd \\<nas>\docker\tcgstockbot
.\docs\add-screenshot.ps1 -ImagePath "C:\path\to\image.png" -OutputName "dashboard-products"
```

The script:
1. Shows a security checklist
2. Asks you to confirm you removed anything sensitive
3. Copies the image to `docs/screenshots/`
4. Commits it
5. Pushes to GitHub

### Option 2: by hand

1. **Clean the image** (remove sensitive data):
   - Open the screenshot in Paint, GIMP or https://www.pixlr.com/
   - Blur or crop out emails, phone numbers, IPs, URLs with ports and credentials
   - Save it as PNG

2. **Copy it to the right folder**:
   ```powershell
   Copy-Item -Path "C:\path\image.png" -Destination "docs\screenshots\dashboard-products.png"
   ```

3. **Commit and push**:
   ```powershell
   git add docs/screenshots/dashboard-products.png
   git commit -m "docs: add screenshot - dashboard-products"
   git push origin main
   ```

The file names in use are listed in [`docs/screenshots/README.md`](docs/screenshots/README.md).

## Security checklist

Before committing, check that you removed:

- [ ] Emails
- [ ] Phone numbers
- [ ] Local IPs (e.g. 192.168.x.x)
- [ ] URLs with ports (e.g. localhost:5080)
- [ ] User names
- [ ] Credentials, API keys and tokens
- [ ] Personal data
- [ ] Anything else sensitive

## If you make a mistake

If you commit a screenshot with sensitive data by accident:

```powershell
# Not pushed yet: undo the last commit and remove the file
git reset HEAD~1
git reset -- docs/screenshots/problematic-file.png
rm docs/screenshots/problematic-file.png

# Already pushed: the file must also be removed from the history (git filter-repo or BFG),
# and anything exposed (keys, tokens) should be rotated.
```
