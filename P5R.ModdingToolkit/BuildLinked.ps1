# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/P5R.ModdingToolkit/*" -Force -Recurse
dotnet publish "./P5R.ModdingToolkit.csproj" -c Release -o "$env:RELOADEDIIMODS/P5R.ModdingToolkit" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location