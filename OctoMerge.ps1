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

$procArgs = $args | %{
  $_.replace('\','/')
}

RunNativeCommand "docker pull $registry$image"
RunNativeCommand "docker run -it -v $([Environment]::CurrentDirectory)`:/app/data --rm -w=/app/data $registry$image $procArgs"
