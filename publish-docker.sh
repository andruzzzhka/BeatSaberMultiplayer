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

docker build -t andruzzzhka/serverhub:latest .

# Tag This Build
docker tag andruzzzhka/serverhub andruzzzhka/serverhub:$tag

# Push Core
docker push andruzzzhka/serverhub:latest
