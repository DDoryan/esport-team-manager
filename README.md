# Esport Team Manager

Application web de gestion d’équipes esport : comptes confirmés par courriel, équipes, calendrier partagé et activités planifiées.

## État du projet

- **Jalon atteint :** P0 terminé le 23 août 2026.
- **Version de référence :** release GitHub de préversion `v0.1.0-p0`, publiée sur le commit vérifié `ab326f6` après la clôture documentaire.
- **Production :** <https://esport-team-manager-production.up.railway.app>
- **Dernier ticket clôturé :** ACC-004 le 25 août 2026, en 2 h 30 pour 4 h estimées.
- **Validation :** 122 tests automatisés réussis localement sous WSL2 et dans GitHub Actions, Smart App Control maintenu actif, CI de `master` verte, déploiement manuel contrôlé réussi et récupération du mot de passe validée en production sur mobile.

## Architecture

- ASP.NET Core MVC et .NET 10 ;
- Entity Framework Core ;
- SQLite pour le développement local et les tests ;
- PostgreSQL pour la production ;
- migrations PostgreSQL dans un projet dédié ;
- ASP.NET Core Identity, clés Data Protection persistées et fournisseur de jeton de réinitialisation dédié d’une heure ;
- Brevo pour les courriels transactionnels ;
- Docker pour la construction et Railway pour l’hébergement.
- journaux techniques corrélés et traces persistantes des actions sensibles, sans données personnelles ou secrets inutiles ;
- traitement de `X-Forwarded-Proto` avant le pipeline et redirection HTTPS publique déléguée à Railway.

## Prérequis locaux

- SDK .NET 10 compatible avec la solution ;
- Visual Studio 2026 ou un environnement compatible .NET 10 ;
- SQLite ;
- Git ;
- pour exécuter les tests sur un poste où Smart App Control bloque les bibliothèques SQLite non signées : WSL2 avec Ubuntu 24.04 et le SDK .NET 10.

## Installation locale

1. Cloner le dépôt puis ouvrir `RepriseWeb.slnx`.
2. Vérifier que le projet web est le projet de démarrage.
3. Restaurer les dépendances :

   ```powershell
   dotnet restore
   ```

4. Appliquer les migrations SQLite :

   ```powershell
   dotnet ef database update --project .\EsportTeamManager.Infrastructure\EsportTeamManager.Infrastructure.csproj --startup-project .\EsportTeamManager.Web.csproj --context ApplicationDbContext
   ```

5. Lancer l’application :

   ```powershell
   dotnet run --project .\EsportTeamManager.Web.csproj
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

Sur Railway, `UseForwardedHeaders()` traite uniquement `X-Forwarded-Proto`. `UseHttpsRedirection()` reste actif hors Railway mais est ignoré lorsque `RAILWAY_PROJECT_ID` est défini : Railway termine TLS et assure lui-même la redirection HTTP publique, tandis que ses requêtes internes ne doivent pas chercher un port HTTPS Kestrel.


### Déploiement manuel contrôlé

La production Railway reste liée à `master`, mais l’auto-déploiement est désactivé. Après une CI `master` verte et une sauvegarde PostgreSQL vérifiée, le déploiement est déclenché depuis GitHub Actions avec le workflow **Déployer manuellement en production**.

Le workflow refuse toute autre branche, restaure, compile, exécute les 122 tests, audite les dépendances, lance Railway en mode attaché, attend la fin réelle du déploiement puis vérifie `/health`. Le secret `RAILWAY_TOKEN` est limité au projet et à l’environnement de production et n’est jamais versionné.

Sur l’offre Railway actuelle, les sauvegardes natives ne sont pas disponibles. Avant une migration à risque, créer une sauvegarde logique indépendante avec une version de `pg_dump` compatible avec PostgreSQL 18, vérifier qu’elle est non vide et lisible par `pg_restore`, puis conserver son empreinte SHA-256 hors Railway. QLT-008 doit automatiser cette sauvegarde et valider une restauration réelle.


### Récupération du mot de passe

Depuis la page de connexion, le lien **Mot de passe oublié ?** ouvre une demande à réponse neutre : l’application ne révèle pas si l’adresse correspond à un compte. Pour un compte confirmé, au plus trois courriels sont envoyés par heure.

Le lien reçu par Brevo est protégé par ASP.NET Core Identity et Data Protection. Il expire après une heure et ne peut être utilisé qu’une fois. Après la saisie et la confirmation d’un mot de passe conforme, l’utilisateur revient à la connexion ; aucune session n’est créée automatiquement.

Les migrations `AddPasswordResetEmailRateLimit` existent pour SQLite et PostgreSQL. La production a appliqué `20260825080814_AddPasswordResetEmailRateLimit` lors du déploiement contrôlé d’ACC-004.

## Compilation et tests

```powershell
dotnet build RepriseWeb.slnx
dotnet test EsportTeamManager.Tests/EsportTeamManager.Tests.csproj
```

Après ACC-004 : six projets compilés sans avertissement ni erreur ; 122 tests automatisés réussis localement sous WSL2 et dans GitHub Actions ; workflow manuel de production, migrations SQLite/PostgreSQL et parcours Brevo/mobile validés.

### Exécution locale des tests avec Smart App Control

Sous Windows, Smart App Control peut empêcher `testhost.exe` de charger la bibliothèque non signée `SQLitePCLRaw.batteries_v2.dll`. Le chargement échoue alors avec le code `0x800711C7`, sans remettre en cause le code des tests ni leur résultat dans la CI Linux.

Smart App Control ne doit pas être désactivé. Les tests sont exécutés localement dans Ubuntu 24.04 sous WSL2, avec le SDK .NET 10.

Lors de la première utilisation, créer une copie Linux propre du dépôt en excluant les sorties générées sous Windows :

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0 rsync
mkdir -p ~/source/repos/esport-team-manager
rsync -a --exclude='.vs/' --exclude='bin/' --exclude='obj/' /mnt/c/Users/<utilisateur-windows>/source/repos/esport-team-manager/ ~/source/repos/esport-team-manager/
cd ~/source/repos/esport-team-manager
dotnet test RepriseWeb.slnx
```

Avant les exécutions suivantes, resynchroniser la copie Linux depuis le dépôt Windows :

```bash
rsync -a --delete --exclude='.git/' --exclude='.vs/' --exclude='bin/' --exclude='obj/' /mnt/c/Users/<utilisateur-windows>/source/repos/esport-team-manager/ ~/source/repos/esport-team-manager/
cd ~/source/repos/esport-team-manager
dotnet test RepriseWeb.slnx
```

La copie Linux conserve son propre dossier `.git`. L’option `--delete` aligne les fichiers de travail sans supprimer ce dossier ni modifier le dépôt Windows.

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