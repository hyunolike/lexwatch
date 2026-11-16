# 빌드 컨텍스트는 저장소 루트다: docker build -f backend/Dockerfile .
# (재현 실행 기록 fixtures/demo/replays를 csproj가 실행 파일에 포함하기 때문이다)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/global.json backend/
COPY backend/src/LexWatch.Api/LexWatch.Api.csproj backend/src/LexWatch.Api/
RUN cd backend/src/LexWatch.Api && dotnet restore
COPY backend/src backend/src
COPY fixtures fixtures
RUN cd backend/src/LexWatch.Api && dotnet publish -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Render 무료 플랜은 512MB다. 힙을 256MB(0x10000000)로 묶고, ICU 없이 동작하게 한다.
# DOTNET_ 환경변수의 GC 값은 16진수다.
ENV DOTNET_GCHeapHardLimit=10000000 \
    DOTNET_gcServer=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_TieredPGO=0 \
    DOTNET_EnableDiagnostics=0
USER $APP_UID
# Render는 PORT를 넣어 준다. 없으면 8080.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet LexWatch.Api.dll"]
