#!/usr/bin/env bash

echo Please enter release tag:
read tag

echo "Please confirm you would like to release with tag: $tag"
echo "(y/N)"
read confirm
echo

if [[ ! $confirm =~ ^[Yy]$ ]]
then
  exit 1
fi

docker build -t andruzzzhka/bsmultiplayer-core:latest -f core.Dockerfile .
docker build -t andruzzzhka/bsmultiplayer-hub:latest -f hub.Dockerfile .
docker build -t andruzzzhka/bsmultiplayer-server:latest -f server.Dockerfile .

# Tag This Build
docker tag andruzzzhka/bsmultiplayer-hub andruzzzhka/bsmultiplayer-hub:$tag
docker tag andruzzzhka/bsmultiplayer-server andruzzzhka/bsmultiplayer-server:$tag

# Push Core
docker push andruzzzhka/bsmultiplayer-core:latest

# Push Hub
docker push andruzzzhka/bsmultiplayer-hub:latest
docker push andruzzzhka/bsmultiplayer-hub:$tag

# Push Server
docker push andruzzzhka/bsmultiplayer-server:latest
docker push andruzzzhka/bsmultiplayer-server:$tag
