# Run a dotnet build container
FROM microsoft/dotnet:2.1-sdk as builder
WORKDIR /app

# Copy project source to builder
COPY . ./

# Restore Project Packages
RUN dotnet restore

# andruzzzhka

# Enter the Server Project and build
RUN cd ServerHub && \
  dotnet publish -f netcoreapp2.0 -o out -r linux-x64 && \
  cd out && \
  chmod +x ServerHub

# Standalone Linux Container
FROM andruzzzhka/bsmultiplayer-core:latest

# Copy built files
WORKDIR /app
COPY --from=builder /app/ServerHub/out .

# Start the process
EXPOSE 3700
CMD ["./ServerHub"]
