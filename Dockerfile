# ==========================================
# Étape 1 : Compilation et création du Bundle
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# 1. On copie tout le code source d'un coup
COPY . ./

# 2. Nettoyage des dossiers de ta machine locale avant de restaurer
RUN rm -rf devopsnet/obj devopsnet/bin

# 3. Restauration propre des packages dans le conteneur
RUN dotnet restore devopsnet/devopsnet.csproj

# 4. Installation d'Entity Framework Core CLI tool
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"

#  FIX : Fausse chaîne de connexion temporaire pour que Program.cs ne plante pas au build
ENV ConnectionStrings__PostgresConnection="Server=localhost;Database=dummy;User Id=dummy;Password=dummy;"

# 5. Génération du bundle autonome de migration
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

# Exécution des vraies migrations (avec la vraie BDD) puis démarrage de l'API
ENTRYPOINT ["/bin/sh", "-c", "./migrate --connection \"$ConnectionStrings__PostgresConnection\" && dotnet devopsnet.dll"]