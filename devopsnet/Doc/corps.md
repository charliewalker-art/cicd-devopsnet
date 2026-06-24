# Module CORS

## Fichiers concernés

| Fichier | Dossier |
|---|---|
| `CorsOptions.cs` | `Options/` |
| `CorsServiceExtensions.cs` | `Options/` |

---

## Rôle

Autorise le frontend React (origine différente de l'API, ex. `http://localhost:5173` vs `https://localhost:7198`) à appeler l'API, tout en restant restreint à une seule origine de confiance configurable — pas une ouverture à tout le web.

---

## `Options/CorsOptions.cs`

Classe de configuration fortement typée, liée à la section `Cors` de la configuration.

| Champ | Type | Source |
|---|---|---|
| `AllowedOrigin` | `string` | `CORS_ALLOWED_ORIGIN` dans `.env`, injecté dans `Program.cs` via `builder.Configuration["Cors:AllowedOrigin"]` |

Une seule valeur attendue dans le `.env`, peu importe le protocole utilisé (`http://` ou `https://`) :
```env
CORS_ALLOWED_ORIGIN=http://localhost:5173
```

---

## `Options/CorsServiceExtensions.cs`

Méthode d'extension qui encapsule toute la configuration CORS, pour garder `Program.cs` court et lisible — même logique que `GitHubOptions` côté organisation.

**`AddReactCorsPolicy(IServiceCollection, IConfiguration)` → `IServiceCollection`**

1. Lit `Cors:AllowedOrigin` depuis la configuration
2. Lève `InvalidOperationException` si la valeur est absente (échec rapide au démarrage plutôt qu'un bug silencieux plus tard)
3. Extrait l'autorité (`host:port`) de l'URL fournie via `Uri.Authority`
4. Reconstruit automatiquement les deux variantes `http://{authority}` et `https://{authority}`
5. Enregistre une policy CORS nommée (`PolicyName = "ReactApp"`) autorisant ces deux origines, tous les headers, toutes les méthodes HTTP, et les credentials

**Pourquoi reconstruire les deux variantes plutôt que de les lister dans le `.env`** : Vite peut démarrer indifféremment en `http://` ou `https://` selon la configuration locale. Une seule valeur dans le `.env` suffit ; le code se charge de couvrir les deux cas sans duplication de configuration.

**Pourquoi pas `AllowAnyOrigin()`** : incompatible avec `AllowCredentials()` au niveau du navigateur (le navigateur rejette la réponse si les deux sont actifs en même temps), et ouvrirait l'API à n'importe quel site tiers capable de faire exécuter une requête depuis le navigateur d'un utilisateur connecté.

---

## Utilisation dans `Program.cs`

```csharp
// Injection de la variable d'environnement
builder.Configuration["Cors:AllowedOrigin"] = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGIN");

// Enregistrement de la policy
builder.Services.AddReactCorsPolicy(builder.Configuration);

// Activation dans le pipeline — avant UseAuthentication()
app.UseCors(CorsServiceExtensions.PolicyName);
```

**Ordre important dans le pipeline** : `UseCors` doit être appelé avant `UseAuthentication()` et `UseAuthorization()`. Sinon, les requêtes de pré-vérification du navigateur (`OPTIONS`, dites "preflight") seraient bloquées par l'authentification avant même d'atteindre la vérification CORS, et React ne recevrait jamais de réponse exploitable.

---

## Si le port du frontend change

Modifier uniquement la valeur de `CORS_ALLOWED_ORIGIN` dans `.env` — aucune recompilation du code C# nécessaire.