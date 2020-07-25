param ([string]$rid = "win-x64",[string]$tag)

$ErrorActionPreference = "Stop"
$global:ProgressPreference = "SilentlyContinue"

$targetFramework = "netcoreapp3.1"

$targetFolder = Join-Path $PSScriptRoot "Build"
$debugBin = Join-Path $PSScriptRoot "bin\Debug\$targetFramework"

$releasePublish = Join-Path $PSScriptRoot "bin\Release\$targetFramework\$rid\publish"

if (!$tag) {  
  $project = Join-Path $PSScriptRoot OctoMerge.csproj 
  $tag = ([xml](Get-Content $project)).Project.PropertyGroup[1].AssemblyVersion
}

$targetSelfContainedZip = Join-Path $targetFolder "octomerge-$rid-$tag.zip"
$targetSelfContainedTgz = Join-Path $targetFolder "octomerge-$rid-$tag.tgz"

mkdir $targetFolder -force | Out-Null

dotnet publish -c Release -r $rid -p:PublishTrimmed=true -p:PublishSingleFile=true
Compress-Archive "$releasePublish/*" $targetSelfContainedZip -Force
Push-Location
cd $releasePublish
tar -czf $targetSelfContainedTgz *
Pop-Location
