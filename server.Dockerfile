# Run a dotnet build container
FROM microsoft/dotnet:2.1-sdk as builder
WORKDIR /app

# Copy project source to builder
COPY . ./
# Restore Project Packages
RUN dotnet restore

# andruzzzhka

# Enter the Server Project and build
RUN cd BeatSaberMultiplayerServer && \
  dotnet publish -f netcoreapp2.0 -o out -r linux-x64 && \
  cd out && \
  chmod +x MultiplayerServer

# Standalone Linux Container
FROM andruzzzhka/bsmultiplayer-core:latest

# Copy built files
WORKDIR /app
COPY --from=builder /app/BeatSaberMultiplayerServer/out .

# Start the process
EXPOSE 3701
CMD ["./MultiplayerServer"]
