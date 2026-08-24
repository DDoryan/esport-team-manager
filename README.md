# Esport Team Manager

Application web de gestion d’équipes esport : comptes confirmés par courriel, équipes, calendrier partagé et activités planifiées.

## État du projet

- **Jalon atteint :** P0 terminé le 23 août 2026.
- **Version de référence :** release GitHub de préversion `v0.1.0-p0`, publiée sur le commit vérifié `ab326f6` après la clôture documentaire.
- **Production :** <https://esport-team-manager-production.up.railway.app>
- **Dernier ticket clôturé :** QLT-013 le 24 août 2026, en 1 h 30 pour 2 h estimées.
- **Validation :** 111 tests automatisés réussis, CI GitHub Actions verte, déploiement Railway opérationnel, `/health` sain et aucun avertissement HTTPS dans les nouveaux logs.

## Architecture

- ASP.NET Core MVC et .NET 10 ;
- Entity Framework Core ;
- SQLite pour le développement local et les tests ;
- PostgreSQL pour la production ;
- migrations PostgreSQL dans un projet dédié ;
- ASP.NET Core Identity et clés Data Protection persistées en base ;
- Brevo pour les courriels transactionnels ;
- Docker pour la construction et Railway pour l’hébergement ;
- journaux techniques corrélés et traces persistantes des actions sensibles, sans données personnelles ou secrets inutiles ;
- traitement de `X-Forwarded-Proto` avant le pipeline et redirection HTTPS publique déléguée à Railway.

## Prérequis locaux

- SDK .NET 10 compatible avec la solution ;
- Visual Studio 2026 ou un environnement compatible .NET 10 ;
- SQLite ;
- Git.

## Installation locale

1. Cloner le dépôt puis ouvrir `RepriseWeb.slnx`.
2. Vérifier que le projet web est le projet de démarrage.
3. Restaurer les dépendances :

   ```powershell
   dotnet restore
   ```

4. Appliquer les migrations SQLite :

   ```powershell
   dotnet ef database update --project EsportTeamManager.Infrastructure --startup-project EsportTeamManager.Web --context ApplicationDbContext
   ```

5. Lancer l’application :

   ```powershell
   dotnet run --project EsportTeamManager.Web
   ```

En développement, les courriels sont conservés par `DevelopmentEmailService` et aucun secret Brevo n’est nécessaire.

## Configuration de production

Les secrets ne doivent jamais être versionnés. La production attend au minimum :

- `ASPNETCORE_ENVIRONMENT=Production` ;
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` ;
- `ConnectionStrings__DefaultConnection` ;
- `Brevo__ApiKey` ;
- `Brevo__SenderEmail` ;
- `Brevo__SenderName`.

La chaîne PostgreSQL de production comprend `GSS Encryption Mode=Disable` : le service n’utilise pas Kerberos et conserve le chiffrement TLS sans tenter de charger les bibliothèques GSS absentes du conteneur.

Le service expose `/health` pour le contrôle de disponibilité. Le Dockerfile attendu par Railway se trouve à la racine : `/Dockerfile`.

## Compilation et tests

```powershell
dotnet build RepriseWeb.slnx
dotnet test EsportTeamManager.Tests/EsportTeamManager.Tests.csproj
```

Après QLT-013 : six projets compilés sans avertissement ni erreur et 111 tests automatisés réussis dans GitHub Actions.

Sur le poste Windows actuel, Smart App Control peut bloquer le chargement de certaines DLL de test avec l’erreur `0x800711C7`. Ne pas désactiver cette protection : la CI Linux constitue la preuve de validation et QLT-014 doit restaurer l’exécution locale complète.

Sur Railway, `UseForwardedHeaders()` traite uniquement `X-Forwarded-Proto`. `UseHttpsRedirection()` reste actif hors Railway mais est ignoré lorsque `RAILWAY_PROJECT_ID` est défini : Railway termine TLS et assure lui-même la redirection HTTP publique, tandis que ses requêtes internes ne doivent pas chercher un port HTTPS Kestrel.

## Documentation

Le dossier documentaire de référence comprend notamment :

- dossier de projet et dossier de continuité ;
- architecture, besoins, modèles de données et wireframes fonctionnels ;
- backlog, planning et plan de tests ;
- dossier sécurité/RGPD ;
- documentation de déploiement/exploitation ;
- procédures de sauvegarde, restauration et incident ;
- rapport accessibilité/écoconception ;
- guide utilisateur et scénario de démonstration ;
- journal de veille et changelog.

Les mentions légales, CGU, politique de confidentialité et registre des traitements RGPD doivent être créés pendant le P1 dès stabilisation des traitements, avant toute ouverture publique élargie.