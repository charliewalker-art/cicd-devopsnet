# Entité User

## Fichiers concernés

| Fichier | Dossier |
|---|---|
| `User.cs` | `Models/` |
| `UserCreateDto.cs` | `Dto/` |
| `UserResponseDto.cs` | `Dto/` |
| `LoginRequestDto.cs` | `Dto/` |
| `AuthResponseDto.cs` | `Dto/` |
| `UserQueries.cs` | `Data/` |
| `UserService.cs` | `Services/` |
| `AuthService.cs` | `Services/` |
| `TokenService.cs` | `Services/` |
| `AuthController.cs` | `Controllers/` |

---

## Modèle — `Models/User.cs`

| Champ | Type C# | Type PostgreSQL | Contrainte |
|---|---|---|---|
| `Id` | `Guid` | `uuid` | Clé primaire |
| `Username` | `string` | `text` | Unique, non null |
| `Email` | `string` | `text` | Unique, non null |
| `PasswordHash` | `string` | `text` | Non null — jamais exposé en dehors du Service |
| `CreatedAt` | `DateTime` | `timestamp with time zone` | Non null |
| `GitHubAccount` | `GitHubAccount?` | — | Navigation, pas une colonne |

---

## DTOs et exemples JSON

### `UserCreateDto.cs` — entrée de `POST /api/auth/register`

| Champ | Type JSON |
|---|---|
| `username` | string |
| `email` | string |
| `password` | string |

Exemple de requête que le frontend doit envoyer :
```json
{
  "username": "charlie",
  "email": "charlie@example.com",
  "password": "MotDePasse123!"
}
```

### `UserResponseDto.cs` — sortie après création réussie

| Champ | Type JSON |
|---|---|
| `id` | string (GUID) |
| `username` | string |
| `email` | string |
| `createdAt` | string (date ISO 8601) |

Exemple de réponse `201 Created` :
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "charlie",
  "email": "charlie@example.com",
  "createdAt": "2026-06-21T10:42:00Z"
}
```

Réponse d'erreur possible — `409 Conflict` (username ou email déjà pris) :
```json
{
  "message": "Ce nom d'utilisateur est déjà pris."
}
```

### `LoginRequestDto.cs` — entrée de `POST /api/auth/login`

| Champ | Type JSON |
|---|---|
| `username` | string |
| `password` | string |

Exemple de requête :
```json
{
  "username": "charlie",
  "password": "MotDePasse123!"
}
```

### `AuthResponseDto.cs` — sortie après login réussi

| Champ | Type JSON |
|---|---|
| `token` | string (JWT) |
| `expiresAt` | string (date ISO 8601) |
| `user` | objet `UserResponseDto` |

Exemple de réponse `200 OK` :
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-06-21T11:42:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "charlie",
    "email": "charlie@example.com",
    "createdAt": "2026-06-21T10:42:00Z"
  }
}
```

Réponse d'erreur possible — `401 Unauthorized` :
```json
{
  "message": "Identifiants invalides."
}
```

**Important pour le frontend** : le `token` reçu doit être stocké côté client (ex. en mémoire ou stockage sécurisé) et renvoyé dans le header `Authorization: Bearer {token}` pour **toutes** les requêtes vers les routes protégées (`/api/auth/github/*` et `/api/repositories/*`).

---

## Requêtes — `Data/UserQueries.cs`

| Méthode | Usage |
|---|---|
| `ByUsername(string)` | Login, vérification d'unicité à la création |
| `ByEmail(string)` | Vérification d'unicité à la création |
| `ById(Guid)` | Récupération à partir de l'Id contenu dans le JWT |
| `WithGitHubAccount()` | Charge la relation `GitHubAccount` via `.Include()` |

---

## Services

### `Services/UserService.cs`

**`CreateAsync(UserCreateDto)` → `UserResponseDto`**
1. Vérifie l'unicité de `Username` (sinon `InvalidOperationException`)
2. Vérifie l'unicité de `Email` (sinon `InvalidOperationException`)
3. Hache `Password` avec BCrypt → `PasswordHash`
4. Crée l'entité, `SaveChangesAsync()`
5. Mappe vers `UserResponseDto`

**`GetEntityByIdAsync(Guid)` → `User?`**
- Retourne l'entité brute — usage interne réservé aux autres Services, jamais appelé depuis un Controller

### `Services/AuthService.cs`

**`LoginAsync(LoginRequestDto)` → `AuthResponseDto`**
1. Cherche par `Username`
2. Vérifie `Password` avec `BCrypt.Verify` contre `PasswordHash`
3. Si échec à l'une des deux étapes → `UnauthorizedAccessException` (message générique, ne précise jamais lequel des deux est en cause)
4. Génère le token via `TokenService.GenerateToken`
5. Retourne `AuthResponseDto`

### `Services/TokenService.cs`

**`GenerateToken(User)` → `(string Token, DateTime ExpiresAt)`**
- Claims inclus : `Sub` (= `User.Id`), `UniqueName` (= `Username`), `Email`
- Signature HMAC SHA256 avec `Jwt:SecretKey`
- Expiration calculée selon `Jwt:ExpirationMinutes`

---

## Contrôleur — `Controllers/AuthController.cs`

| Route | Méthode HTTP | Auth requise | Succès | Échec |
|---|---|---|---|---|
| `/api/auth/register` | `POST` | Non | `201 Created` + `UserResponseDto` | `409 Conflict` |
| `/api/auth/login` | `POST` | Non | `200 OK` + `AuthResponseDto` | `401 Unauthorized` |

---

## Flux de données complet

### Inscription — `POST /api/auth/register`
```
Frontend
  │  POST { username, email, password }
  ▼
AuthController.Register
  │  désérialise UserCreateDto
  ▼
UserService.CreateAsync
  │  1. UserQueries.ByUsername → vérifie unicité
  │  2. UserQueries.ByEmail → vérifie unicité
  │  3. BCrypt.HashPassword(password)
  │  4. _context.Users.Add(...) + SaveChangesAsync()
  │  5. mappe en UserResponseDto
  ▼
AppDbContext → PostgreSQL (table Users)
  ▼
AuthController
  │  retourne 201 Created + UserResponseDto
  ▼
Frontend
```

### Connexion — `POST /api/auth/login`
```
Frontend
  │  POST { username, password }
  ▼
AuthController.Login
  │  désérialise LoginRequestDto
  ▼
AuthService.LoginAsync
  │  1. UserQueries.ByUsername → récupère l'entité User
  │  2. BCrypt.Verify(password, user.PasswordHash)
  │  3. TokenService.GenerateToken(user) → JWT
  │  4. mappe en AuthResponseDto
  ▼
AuthController
  │  retourne 200 OK + { token, expiresAt, user }
  ▼
Frontend
  │  stocke le token, l'utilisera en Authorization: Bearer {token}
```

---

## Tests — `devopsnet.Tests/Services/`

À couvrir :
- `UserServiceTests.cs` : création réussie, rejet si username déjà pris, rejet si email déjà pris, vérification que `PasswordHash` ≠ `Password` en clair
- `AuthServiceTests.cs` : login réussi avec token généré, rejet si username inexistant, rejet si mot de passe incorrect