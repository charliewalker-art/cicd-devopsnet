# ==========================================
# Étape 1 : Compilation et création du Bundle
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# 1. On copie tout le code source
COPY . ./

# 2. Nettoyage des résidus locaux pour éviter les conflits Windows/Linux
RUN rm -rf devopsnet/obj devopsnet/bin

# 3. Restauration propre des packages NuGet
RUN dotnet restore devopsnet/devopsnet.csproj

# 4. Installation de l'outil Entity Framework Core CLI
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"

#  LA CORRECTION DE CLAUDE : Une variable d'environnement avec la syntaxe STRICTE de PostgreSQL
# Le double underscore '__' remplace le ':' pour que .NET le lise nativement.
ENV ConnectionStrings__PostgresConnection="Host=localhost;Port=5432;Database=design_time;Username=dummy;Password=dummy"

# 5. Génération du bundle autonome (le Program.cs va démarrer sans crash grâce à l'ENV ci-dessus)
RUN dotnet ef migrations bundle --project devopsnet/devopsnet.csproj --startup-project devopsnet/devopsnet.csproj -o out/migrate --verbose

# 6. Publication finale de l'API
RUN dotnet publish devopsnet/devopsnet.csproj -c Release -o out --no-restore

# ==========================================
# Étape 2 : Image finale légère pour l'exécution
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# On récupère l'API publiée et le bundle migrate
COPY --from=build-env /app/out .

# Droits d'exécution pour le binaire de migration automatique
RUN chmod +x ./migrate

# Alignement sur ton port d'écoute 7198
EXPOSE 7198
ENV ASPNETCORE_URLS=http://+:7198

# Au runtime, la vraie variable d'environnement écrasera la valeur fictive du build
ENTRYPOINT ["/bin/sh", "-c", "./migrate --connection \"$ConnectionStrings__PostgresConnection\" && dotnet devopsnet.dll"]