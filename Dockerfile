# Run a dotnet build container
FROM microsoft/dotnet:2.1-sdk as builder
WORKDIR /app

# Copy project source to builder
COPY . ./

# Restore Project Packages
RUN dotnet restore

# Enter the Server Project and build
RUN cd ServerHub && \
  dotnet publish -f netcoreapp2.0 -o out -r linux-x64 && \
  cd out && \
  chmod +x ServerHub

FROM debian:stretch-slim
RUN apt-get update \
  && apt-get install -y --no-install-recommends \
    ca-certificates netcat \
    \
# .NET Core dependencies
    libc6 \
    libcurl3 \
    libgcc1 \
    libgssapi-krb5-2 \
    libicu57 \
    liblttng-ust0 \
    libssl1.0.2 \
    libstdc++6 \
    libunwind8 \
    libuuid1 \
    zlib1g \
&& rm -rf /var/lib/apt/lists/*

# Enable detection of running in a container
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Copy built files
WORKDIR /app
COPY --from=builder /app/ServerHub/out .

# Start the process
HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=1 CMD nc -w 5 -uvz localhost 3700
EXPOSE 3700/udp
CMD ["./ServerHub"]
