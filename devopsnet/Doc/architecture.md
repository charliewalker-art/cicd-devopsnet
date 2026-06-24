# devopsnet — Architecture

## Structure de la solution

```
Solution 'devopsnet'
│
├── devopsnet/
│   ├── Controllers/
│   ├── Data/
│   ├── Doc/
│   │   ├── architecture.md
│   │   └── entities/
│   ├── Dto/
│   ├── Models/
│   ├── Options/
│   ├── Services/
│   └── Program.cs
│
└── devopsnet.Tests/
    └── Services/
```

---

## Rôle des dossiers et fichiers

### `Models/`

Entités C# représentant exactement les tables PostgreSQL. Utilisées exclusivement par Entity Framework Core. Ne sont jamais sérialisées ni renvoyées directement au client.

### `Data/`

`AppDbContext.cs` — passerelle entre le code C# et PostgreSQL. Expose les `DbSet<T>` de chaque table.

`[Entité]Queries.cs` — méthodes d'extension statiques sur `IQueryable<T>`, regroupant toutes les requêtes LINQ propres à une entité. Remplace le pattern Repository : pas d'interface, pas d'injection de dépendance.

### `Dto/`

DTO d'entrée (`[Entité]CreateDto`) — données reçues du client. Peut contenir des champs sensibles (ex. mot de passe en clair). N'inclut jamais d'Id.

DTO de sortie (`[Entité]ResponseDto`) — données renvoyées au client. Omet systématiquement les champs sensibles (hash, tokens internes).

### `Services/`

Reçoit les DTOs d'entrée. Applique la logique métier (validation, hachage, règles de gestion). Utilise les méthodes de `[Entité]Queries.cs` pour interroger la base. Mappe les entités en DTOs de sortie avant de les retourner. Appelle `SaveChangesAsync()` via `AppDbContext`. Seule couche autorisée à manipuler les entités directement.

### `Controllers/`

Reçoit la requête HTTP et désérialise le DTO entrant. Appelle le Service correspondant. Retourne la réponse HTTP appropriée (`200 OK`, `201 Created`, `404 Not Found`, etc.). Ne contient aucune logique métier. Ne touche jamais aux entités de base de données.

### `Options/`

Classes de configuration fortement typées, liées aux sections de `appsettings.json` ou aux variables d'environnement (ex. identifiants client d'un fournisseur externe).

### `Program.cs`

Chargement des variables d'environnement. Configuration de la connexion PostgreSQL via EF Core. Enregistrement des Services dans le conteneur DI (cycle `Scoped`). Démarrage du serveur web ASP.NET Core.

### `Doc/`

`architecture.md` — ce fichier.

`entities/[entité].md` — un fichier par entité, couvrant modèle, DTOs, requêtes, service, contrôleur et tests propres à cette entité.

### `devopsnet.Tests/`

Projet xUnit séparé. Utilise `Microsoft.EntityFrameworkCore.InMemory` pour simuler une base de données éphémère en RAM. Teste la logique métier des Services sans connexion réseau ni état persistant entre les tests.