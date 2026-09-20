# Baut TestGuardian als Release-Paket und installiert es als globales .NET-Tool - ein einziger
# Befehl statt der bisherigen zwei (dotnet pack, dann dotnet tool install). Siehe
# docs/decisions/help-and-install-script.md.

$projectPath = Join-Path $PSScriptRoot "TestGuardian\src\TestGuardian.Console\TestGuardian.Console.csproj"
$nupkgDir = Join-Path $PSScriptRoot "nupkg"

dotnet pack $projectPath -c Release -o $nupkgDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack ist fehlgeschlagen (Exit-Code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# Entfernt eine evtl. vorhandene alte Installation zuerst (Fehler wird bewusst ignoriert, falls
# TestGuardian noch gar nicht installiert war) - macht das Skript beliebig oft wiederholbar,
# z.B. nach Codeaenderungen, ohne "already installed"-Fehler.
dotnet tool uninstall --global TestGuardian *> $null

dotnet tool install --global --add-source $nupkgDir TestGuardian
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet tool install ist fehlgeschlagen (Exit-Code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "TestGuardian wurde installiert. Probier's aus mit: TestGuardian --help"
