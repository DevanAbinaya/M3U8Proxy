FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["M3U8Proxy/M3U8Proxy.csproj", "M3U8Proxy/"]
RUN dotnet restore "M3U8Proxy/M3U8Proxy.csproj"
COPY . .
WORKDIR "/src/M3U8Proxy"
RUN dotnet build "M3U8Proxy.csproj" -c Release -o /app/build

FROM build AS publish
# Add memory optimization for ARM64 build
ENV DOTNET_GCHeapHardLimit=500000000
RUN dotnet publish "M3U8Proxy.csproj" \
    -c Release \
    -o /app/publish \
    -r linux-arm64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:InvariantGlobalization=true \
    /p:PublishTrimmed=false \
    /p:EnableUnsafeBinaryFormatterSerialization=false \
    /p:EnableUnsafeUTF7Encoding=false

FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-bullseye-slim-arm64v8 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./M3U8Proxy"]