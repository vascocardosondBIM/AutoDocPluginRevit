#Requires -Version 5.0
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
dotnet run --project "$PSScriptRoot\_build\_build.csproj" -- @args
exit $LASTEXITCODE
