# Entité GitHubAccount

## Fichiers concernés

| Fichier | Dossier |
|---|---|
| `GitHubAccount.cs` | `Models/` |
| `GitHubOptions.cs` | `Options/` |
| `GitHubCallbackDto.cs` | `Dto/` |
| `RepositoryResponseDto.cs` | `Dto/` |
| `CloneRequestDto.cs` | `Dto/` |
| `GitHubAccountQueries.cs` | `Data/` |
| `GitHubAuthService.cs` | `Services/` |
| `GitHubRepositoryService.cs` | `Services/` |
| `GitCloneService.cs` | `Services/` |
| `GitHubAuthController.cs` | `Controllers/` |
| `RepositoryController.cs` | `Controllers/` |

---

## Modèle — `Models/GitHubAccount.cs`

| Champ | Type C# | Type PostgreSQL | Contrainte |
|---|---|---|---|
| `Id` | `Guid` | `uuid` | Clé primaire |
| `UserId` | `Guid` | `uuid` | Clé étrangère vers `Users.Id`, unique (relation 1-à-1), `ON DELETE CASCADE` |
| `GitHubId` | `long` | `bigint` | Unique, non null — identifiant immuable GitHub |
| `GitHubUsername` | `string` | `text` | Non null — peut changer dans le temps |
| `EncryptedAccessToken` | `string` | `text` | Non null — chiffré via `IDataProtector`, jamais en clair |
| `ConnectedAt` | `DateTime` | `timestamp with time zone` | Non null |
| `User` | `User` | — | Navigation, pas une colonne |

---

## DTOs et exemples JSON

### `GitHubCallbackDto.cs` — entrée de `GET /api/auth/github/callback`

| Champ | Type JSON | Source |
|---|---|---|
| `code` | string | Query string (`?code=...`), envoyé automatiquement par GitHub, pas par le frontend |

Exemple d'appel reçu (le frontend n'a rien à construire ici, c'est GitHub qui redirige) :
```
GET /api/auth/github/callback?code=1a2b3c4d5e6f
Authorization: Bearer {token JWT local}
```

Réponse `200 OK` :
```json
{
  "message": "Compte GitHub lié avec succès."
}
```

Réponse d'erreur `400 Bad Request` :
```json
{
  "message": "Échange du code OAuth2 échoué."
}
```

### `RepositoryResponseDto.cs` — sortie de `GET /api/repositories`

| Champ | Type JSON |
|---|---|
| `name` | string |
| `fullName` | string |
| `cloneUrl` | string |
| `isPrivate` | boolean |
| `defaultBranch` | string |

Exemple de réponse `200 OK` (tableau) :
```json
[
  {
    "name": "devopsnet",
    "fullName": "charlie/devopsnet",
    "cloneUrl": "https://github.com/charlie/devopsnet.git",
    "isPrivate": false,
    "defaultBranch": "main"
  },
  {
    "name": "projet-prive",
    "fullName": "charlie/projet-prive",
    "cloneUrl": "https://github.com/charlie/projet-prive.git",
    "isPrivate": true,
    "defaultBranch": "develop"
  }
]
```

**Important pour le frontend** : `fullName` est la valeur à réutiliser tel quel dans `CloneRequestDto.repoFullName` — ne pas le reconstruire manuellement à partir de `name`.

### `CloneRequestDto.cs` — entrée de `POST /api/repositories/clone`

| Champ | Type JSON |
|---|---|
| `repoFullName` | string (format `owner/repo`) |
| `branch` | string |

Exemple de requête :
```json
{
  "repoFullName": "charlie/projet-prive",
  "branch": "develop"
}
```

Réponse `200 OK` :
```json
{
  "path": "C:\\Temp\\devopsnet-clones\\3fa85f64-5717-4562-b3fc-2c963f66afa6\\7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

Réponse d'erreur `400 Bad Request` (ex. branche inexistante, token expiré) :
```json
{
  "message": "Échec du clonage : ..."
}
```

**Important pour le frontend** : ne jamais envoyer `cloneUrl` directement dans `CloneRequestDto` — ce champ n'existe pas dans ce DTO précisément pour empêcher de cloner une URL arbitraire. Toujours envoyer `repoFullName` + `branch`.

---

## Requêtes — `Data/GitHubAccountQueries.cs`

| Méthode | Usage |
|---|---|
| `ByUserId(Guid)` | Récupère le compte GitHub lié à l'utilisateur connecté |
| `ByGitHubId(long)` | Vérifie si un compte GitHub est déjà lié à un autre utilisateur local |

---

## Services

### `Services/GitHubAuthService.cs`

**`LinkAccountAsync(Guid userId, string code)` → `void`**
1. `ExchangeCodeForTokenAsync(code)` — `POST https://github.com/login/oauth/access_token`
2. `GetGitHubProfileAsync(accessToken)` — `GET https://api.github.com/user`
3. Chiffre le token (`IDataProtector.Protect`)
4. `GitHubAccountQueries.ByUserId` → crée si absent, met à jour si déjà existant
5. `SaveChangesAsync()`

**`GetDecryptedTokenAsync(Guid userId)` → `string`**
1. `GitHubAccountQueries.ByUserId` → récupère l'entité
2. `IDataProtector.Unprotect` → déchiffre
3. Lève `InvalidOperationException` si aucun compte GitHub lié

### `Services/GitHubRepositoryService.cs`

**`GetUserRepositoriesAsync(string accessToken)` → `List<RepositoryResponseDto>`**
- `GET https://api.github.com/user/repos?per_page=100&visibility=all`
- Parcourt la réponse JSON et mappe `name`, `full_name`, `clone_url`, `private`, `default_branch`
- Ne touche jamais à `AppDbContext` ni au chiffrement — reçoit le token déjà déchiffré en paramètre

### `Services/GitCloneService.cs`

**`CloneAsync(string cloneUrl, string branch, string accessToken, Guid userId)` → `string`**
1. Crée `{TEMP}/devopsnet-clones/{userId}/{guid}/`
2. Injecte le token dans l'URL (`https://{token}@github.com/...`)
3. Exécute `git clone --branch {branch} --single-branch "{url}" "{path}"` via `Process`
4. Retourne le chemin local si `ExitCode == 0`, sinon lève `InvalidOperationException` avec le contenu de `StandardError`

⚠️ Point de sécurité connu : le token transite dans les arguments du process système. Acceptable en V1, à revoir avec un credential helper Git pour une version durcie.

---

## Contrôleurs

### `Controllers/GitHubAuthController.cs`

| Route | Méthode HTTP | Auth requise | Succès | Échec |
|---|---|---|---|---|
| `/api/auth/github/login` | `GET` | Oui (JWT local) | `302 Redirect` vers GitHub | — |
| `/api/auth/github/callback` | `GET` | Oui (JWT local) | `200 OK` + message | `400 Bad Request` |

### `Controllers/RepositoryController.cs`

| Route | Méthode HTTP | Auth requise | Succès | Échec |
|---|---|---|---|---|
| `/api/repositories` | `GET` | Oui (JWT local) | `200 OK` + tableau `RepositoryResponseDto` | `400 Bad Request` |
| `/api/repositories/clone` | `POST` | Oui (JWT local) | `200 OK` + `{ path }` | `400 Bad Request` |

**Pré-requis pour ces 4 routes** : header `Authorization: Bearer {token}` obtenu via `POST /api/auth/login`. Sans compte GitHub lié au préalable (`/api/auth/github/callback` réussi), `GetRepositories` et `Clone` échouent avec `"Aucun compte GitHub lié à cet utilisateur."`

---

## Flux de données complet

### Liaison du compte GitHub
```
Frontend (utilisateur déjà connecté localement, JWT en main)
  │  GET /api/auth/github/login   (Authorization: Bearer {jwt})
  ▼
GitHubAuthController.Login
  │  construit l'URL GitHub (client_id, redirect_uri, scope=repo)
  ▼
Redirection navigateur → GitHub (autorisation par l'utilisateur)
  │
  ▼
GitHub redirige vers /api/auth/github/callback?code=...
  ▼
GitHubAuthController.Callback
  │  extrait userId depuis le JWT (claim Sub)
  ▼
GitHubAuthService.LinkAccountAsync(userId, code)
  │  1. échange code → access_token (API GitHub)
  │  2. récupère GitHubId + GitHubUsername (API GitHub)
  │  3. chiffre le token
  │  4. GitHubAccountQueries.ByUserId → create ou update
  │  5. SaveChangesAsync()
  ▼
AppDbContext → PostgreSQL (table GitHubAccounts)
  ▼
GitHubAuthController
  │  retourne 200 OK
  ▼
Frontend
```

### Liste des dépôts — `GET /api/repositories`
```
Frontend
  │  GET /api/repositories   (Authorization: Bearer {jwt})
  ▼
RepositoryController.GetRepositories
  │  extrait userId depuis le JWT
  ▼
GitHubAuthService.GetDecryptedTokenAsync(userId)
  │  GitHubAccountQueries.ByUserId → déchiffre le token
  ▼
GitHubRepositoryService.GetUserRepositoriesAsync(token)
  │  GET api.github.com/user/repos
  │  mappe en List<RepositoryResponseDto>
  ▼
RepositoryController
  │  retourne 200 OK + tableau JSON
  ▼
Frontend
```

### Clonage — `POST /api/repositories/clone`
```
Frontend
  │  POST /api/repositories/clone { repoFullName, branch }   (Authorization: Bearer {jwt})
  ▼
RepositoryController.Clone
  │  extrait userId depuis le JWT
  ▼
GitHubAuthService.GetDecryptedTokenAsync(userId)
  │  récupère le token déchiffré
  ▼
RepositoryController reconstruit cloneUrl = "https://github.com/{repoFullName}.git"
  ▼
GitCloneService.CloneAsync(cloneUrl, branch, token, userId)
  │  exécute git clone via Process
  ▼
Système de fichiers serveur (dossier temporaire)
  ▼
RepositoryController
  │  retourne 200 OK + { path }
  ▼
Frontend
```

---

## Tests — `devopsnet.Tests/Services/`

À couvrir :
- `GitHubAuthServiceTests.cs` : liaison d'un nouveau compte, mise à jour d'un compte existant, chiffrement/déchiffrement cohérent (ce qui est chiffré doit redonner le même token une fois déchiffré)
- `GitHubRepositoryServiceTests.cs` : mapping correct de la réponse GitHub vers `RepositoryResponseDto` (avec `Moq` sur `HttpClient`)
- `GitCloneServiceTests.cs` : gestion de l'échec de clonage (code de sortie non nul → exception avec le bon message), création effective du dossier cible