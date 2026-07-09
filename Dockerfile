FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

COPY . ./
RUN rm -rf devopsnet/obj devopsnet/bin
RUN dotnet restore devopsnet/devopsnet.csproj

RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"

# 1. Fichier de config dans le dossier source (sera copié si le SDK est Microsoft.NET.Sdk.Web)
RUN printf '{\n  "ConnectionStrings": {\n    "PostgresConnection": "Host=localhost;Port=5432;Database=design_time;Username=dummy;Password=dummy"\n  }\n}' > devopsnet/appsettings.json

# 2. Build explicite AVANT le bundle, pour matérialiser le dossier de sortie
RUN dotnet build devopsnet/devopsnet.csproj -c Release --no-restore

# 3. Copie défensive dans TOUS les dossiers de sortie possibles (Debug et Release, au cas où)
RUN find devopsnet/bin -type d -name "net*.0" -exec cp devopsnet/appsettings.json {} \; ; \
    find devopsnet -type d -iname "Debug" -o -iname "Release" 2>/dev/null || true

# 4. On se place directement dans le dossier du projet pour éliminer toute ambiguïté de working directory
WORKDIR /app/devopsnet
RUN dotnet ef migrations bundle --project devopsnet.csproj --startup-project devopsnet.csproj -o ../out/migrate --verbose

WORKDIR /app
RUN rm devopsnet/appsettings.json

RUN dotnet publish devopsnet/devopsnet.csproj -c Release -o out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build-env /app/out .
RUN chmod +x ./migrate
EXPOSE 7198
ENV ASPNETCORE_URLS=http://+:7198

ENTRYPOINT ["/bin/sh", "-c", "./migrate --connection \"$ConnectionStrings__PostgresConnection\" && dotnet devopsnet.dll"]