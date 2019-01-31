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

# Build Docker Image
docker build -t lolpants/serverhub:latest .

# Tag with Version Number
docker tag lolpants/serverhub lolpants/serverhub:$tag

# Push Image
docker push lolpants/serverhub:latest
