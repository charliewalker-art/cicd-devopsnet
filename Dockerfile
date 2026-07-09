# ==========================================
# Étape 1 : Compilation et création du Bundle
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# 1. On copie d'abord ton fichier projet devopsnet pour le cache
COPY devopsnet/devopsnet.csproj ./devopsnet/

# 2. On lance la restauration des packages NuGet
RUN dotnet restore devopsnet/devopsnet.csproj

# 3. Installation d'Entity Framework Core CLI tool
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"

# 4. On copie le reste du code source complet
COPY . ./

# 5. Nettoyage des résidus locaux pour éviter les conflits
RUN rm -rf devopsnet/obj devopsnet/bin

#  CORRECTION ICI : On se déplace dans le dossier de ton projet .NET
WORKDIR /app/devopsnet

# 6. Génération du bundle (plus besoin de spécifier --project car on est dedans !)
# Le résultat est envoyé dans '../out/migrate' (donc dans /app/out/migrate)
RUN dotnet ef migrations bundle -o ../out/migrate --verbose

# 7. Publication finale de l'API de gestion dans '../out' (donc dans /app/out)
RUN dotnet publish devopsnet.csproj -c Release -o ../out --no-restore

# ==========================================
# Étape 2 : Image finale d'exécution légère
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# On récupère les fichiers compilés qui sont bien dans /app/out
COPY --from=build-env /app/out .

# Droits d'exécution pour les migrations automatiques
RUN chmod +x ./migrate

# Alignement sur ton port d'écoute Reverse Proxy
EXPOSE 7198
ENV ASPNETCORE_URLS=http://+:7198

# Lancement des migrations de la BDD puis démarrage de l'API devopsnet
ENTRYPOINT ["/bin/sh", "-c", "./migrate --connection \"$ConnectionStrings__PostgresConnection\" && dotnet devopsnet.dll"]