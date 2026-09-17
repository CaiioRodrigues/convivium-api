# Imagem da API.
#
# Duas etapas: a primeira compila com o SDK inteiro, a segunda leva so o
# resultado. Publicar a partir do SDK deixaria uma imagem com compilador,
# NuGet e codigo-fonte dentro do servidor — peso e superficie de ataque que
# nao servem para nada em producao.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Os arquivos de projeto primeiro, sozinhos: enquanto nenhuma dependencia
# mudar, o Docker reaproveita a camada do restore e a build fica em segundos.
COPY Directory.Packages.props Directory.Build.props Convivium.sln ./
COPY src/Convivium.Domain/*.csproj src/Convivium.Domain/
COPY src/Convivium.Application/*.csproj src/Convivium.Application/
COPY src/Convivium.Infrastructure/*.csproj src/Convivium.Infrastructure/
COPY src/Convivium.Api/*.csproj src/Convivium.Api/
RUN dotnet restore src/Convivium.Api/Convivium.Api.csproj

COPY src/ src/
RUN dotnet publish src/Convivium.Api/Convivium.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# O QuestPDF desenha via SkiaSharp, que precisa das fontes do sistema para
# medir texto. Sem elas o boleto sai com as caixas no lugar das letras.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Usuario sem privilegio: um processo web nao tem por que poder escrever no
# proprio binario.
RUN useradd --system --uid 1001 convivium && chown -R convivium:convivium /app
USER convivium

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=America/Sao_Paulo

EXPOSE 8080

# O compose espera por isto antes de subir o web: a API roda as migrations no
# boot, e o front consultando um banco a meio caminho da migracao daria erro
# que ninguem consegue explicar depois.
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Convivium.Api.dll"]
