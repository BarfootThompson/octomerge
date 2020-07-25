#!/bin/sh

app="octomerge"
registry="index.docker.io/"
image="andrewsav/$app"

docker pull "$registry$image"
docker run -it -v `nano pwd`:/app/data --rm -w=/app/data $registry$image "$@"
