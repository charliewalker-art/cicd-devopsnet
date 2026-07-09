# ==========================================
# Étape 1 : Compilation et création du Bundle
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# 1. Optimisation du cache : On copie d'abord le fichier de projet pour restaurer les packages
COPY momappdonet/momappdonet.csproj ./momappdonet/
# Si tu as un projet de test, décommente la ligne suivante :
# COPY momappdonet.Tests/momappdonet.Tests.csproj ./momappdonet.Tests/

# 2. On lance la restauration des packages (mise en cache par Docker)
RUN dotnet restore momappdonet/momappdonet.csproj

# 3. Installation de l'outil Entity Framework (version 10 pour correspondre au SDK)
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"

# 4. Maintenant on copie tout le reste du code source
COPY . ./

# 5. On supprime les dossiers locaux pour éviter les conflits GitHub Actions
RUN rm -rf momappdonet/obj momappdonet/bin momappdonet.Tests/obj momappdonet.Tests/bin

# 6. Génération du binaire de migration
RUN dotnet ef migrations bundle --project momappdonet/momappdonet.csproj -o out/migrate

# 7. Publication de l'application API
RUN dotnet publish momappdonet/momappdonet.csproj -c Release -o out --no-restore

# ==========================================
# Étape 2 : Image finale légère pour l'exécution
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# On récupère l'application et le bundle de migration
COPY --from=build-env /app/out .

#  FIX : On donne explicitement les droits d'exécution au binaire de migration
RUN chmod +x ./migrate

# On expose le port sur lequel ton API va écouter (Docker Compose / K3s)
EXPOSE 7198
ENV ASPNETCORE_URLS=http://+:7198

# Lancement automatique des migrations puis de l'API
ENTRYPOINT ./migrate --connection "$ConnectionStrings__PostgresConnection" && dotnet momappdonet.dll