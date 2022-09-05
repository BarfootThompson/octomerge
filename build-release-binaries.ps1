param ([string]$rid = "win-x64")

$ErrorActionPreference = "Stop"
$global:ProgressPreference = "SilentlyContinue"

$targetFramework = "net6.0"

$targetFolder = Join-Path $PSScriptRoot "Build"
$debugBin = Join-Path $PSScriptRoot "bin\Debug\$targetFramework"

$releasePublish = Join-Path $PSScriptRoot "bin\Release\$targetFramework\$rid\publish"

mkdir $targetFolder -force | Out-Null

dotnet publish -c Release -r $rid --self-contained -p:PublishSingleFile=true
Copy-Item "$releasePublish/*" $targetFolder -Force
