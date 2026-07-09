# ==========================================
# Étape 1 : Compilation et création du Bundle
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# 1. On copie tout le code source d'un coup
COPY . ./

# 2.  FIX CRITIQUE : On nettoie les dossiers de ta machine AVANT de restaurer
# Comme ça, on ne détruit pas le travail de dotnet restore
RUN rm -rf devopsnet/obj devopsnet/bin

# 3. On lance la restauration propre des packages dans le conteneur
RUN dotnet restore devopsnet/devopsnet.csproj

# 4. Installation d'Entity Framework Core CLI tool
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"

# 5. Génération du bundle (on pointe explicitement le projet depuis la racine)
RUN dotnet ef migrations bundle --project devopsnet/devopsnet.csproj --startup-project devopsnet/devopsnet.csproj -o out/migrate --verbose

# 6. Publication finale de l'API de gestion
RUN dotnet publish devopsnet/devopsnet.csproj -c Release -o out --no-restore

# ==========================================
# Étape 2 : Image finale d'exécution légère
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# On récupère les fichiers compilés (l'API + le bundle migrate)
COPY --from=build-env /app/out .

# Droits d'exécution pour le binaire de pré-déploiement
RUN chmod +x ./migrate

# Configuration du port d'écoute
EXPOSE 7198
ENV ASPNETCORE_URLS=http://+:7198

# Exécution des migrations puis démarrage de l'API devopsnet
ENTRYPOINT ["/bin/sh", "-c", "./migrate --connection \"$ConnectionStrings__PostgresConnection\" && dotnet devopsnet.dll"]