$app = "octomerge"
$registry = "index.docker.io/"
$image = "andrewsav/$app"

function RunNativeCommand {
  param(
    [string]$command
  )
  $command | Write-Host -Foreground Magenta
  iex $command
  if ($LastExitCode -ne 0 ){
    "Last command returned non zero exit code!" | Write-Host -Foreground Red    
    exit 1;
  }
}

[Environment]::CurrentDirectory = (Get-Location -PSProvider FileSystem).ProviderPath
$project = Join-Path $PSScriptRoot OctoMerge.csproj
$version = ([xml](Get-Content $project)).Project.PropertyGroup[1].AssemblyVersion
RunNativeCommand "docker build . -t $registry$image`:$version"
RunNativeCommand "docker tag $registry$image`:$version $registry$image`:latest"
RunNativeCommand "docker push $registry$image`:$version"
RunNativeCommand "docker push $registry$image`:latest"
