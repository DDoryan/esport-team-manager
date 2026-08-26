# Esport Team Manager

Application web de gestion d’équipes esport : comptes confirmés par courriel, équipes, calendrier partagé et activités planifiées.

## État du projet

- **Jalon atteint :** P0 terminé le 23 août 2026.
- **Version de référence :** release GitHub de préversion `v0.1.0-p0`, publiée sur le commit vérifié `ab326f6` après la clôture documentaire.
- **Production :** <https://esport-team-manager-production.up.railway.app>
- **Dernier ticket clôturé :** ACC-006 le 25 août 2026, en 2 h 30 pour 6 h estimées.
- **Avancement :** 24 éléments terminés sur 60 et 182 h estimées restantes.
- **Validation :** 126 tests automatisés réussis localement sous WSL2 et dans GitHub Actions, Smart App Control maintenu actif, CI de `master` verte et production Railway validée après déploiement manuel.

## Architecture

- ASP.NET Core MVC et .NET 10 ;
- Entity Framework Core ;
- SQLite pour le développement local et les tests ;
- PostgreSQL pour la production ;
- migrations PostgreSQL dans un projet dédié ;
- ASP.NET Core Identity et clés Data Protection persistées en base ;
- Brevo pour les courriels transactionnels ;
- Docker pour la construction et Railway pour l’hébergement.
- journaux techniques corrélés et traces persistantes des actions sensibles, sans données personnelles ou secrets inutiles ;
- traitement de `X-Forwarded-Proto` avant le pipeline et redirection HTTPS publique déléguée à Railway.
- déploiement de production déclenché manuellement depuis GitHub Actions après sauvegarde PostgreSQL vérifiée ;
- réinitialisation sécurisée du mot de passe par courriel, limitée à trois demandes par heure, avec jeton d’une heure à usage unique ;
- profil en lecture seule pour l’identité `Pseudo #tag` et changement du mot de passe avec invalidation des autres sessions.

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

Sur Railway, `UseForwardedHeaders()` traite uniquement `X-Forwarded-Proto`. `UseHttpsRedirection()` reste actif hors Railway mais est ignoré lorsque `RAILWAY_PROJECT_ID` est défini : Railway termine TLS et assure lui-même la redirection HTTP publique, tandis que ses requêtes internes ne doivent pas chercher un port HTTPS Kestrel.

Le déploiement automatique Railway est désactivé. La production est mise à jour depuis le workflow GitHub Actions `Déployer manuellement en production`, uniquement depuis `master`, après une CI verte et la création puis la vérification d’une sauvegarde PostgreSQL. Le workflow attend la fin réelle du déploiement Railway avant de contrôler `/health`.

## Compilation et tests

```powershell
dotnet build RepriseWeb.slnx
dotnet test EsportTeamManager.Tests/EsportTeamManager.Tests.csproj
```

Après ACC-005 : six projets compilés sans avertissement ni erreur ; 126 tests automatisés réussis localement sous WSL2 et dans GitHub Actions.

### Exécution locale des tests avec Smart App Control

Sous Windows, Smart App Control peut empêcher `testhost.exe` de charger la bibliothèque non signée `SQLitePCLRaw.batteries_v2.dll`. Le chargement échoue alors avec le code `0x800711C7`, sans remettre en cause le code des tests ni leur résultat dans la CI Linux.

Smart App Control ne doit pas être désactivé. Les tests sont exécutés localement dans Ubuntu 24.04 sous WSL2, avec le SDK .NET 10.

Lors de la première utilisation, créer une copie Linux propre du dépôt en excluant les sorties générées sous Windows :

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0 rsync
mkdir -p ~/source/repos/esport-team-manager
rsync -a --exclude='.vs/' --exclude='bin/' --exclude='obj/' --exclude='*.db' --exclude='*.db-shm' --exclude='*.db-wal' /mnt/c/Users/<utilisateur-windows>/source/repos/esport-team-manager/ ~/source/repos/esport-team-manager/
cd ~/source/repos/esport-team-manager
dotnet test RepriseWeb.slnx
```

Avant les exécutions suivantes, resynchroniser la copie Linux depuis le dépôt Windows :

```bash
rsync -a --delete --exclude='.git/' --exclude='.vs/' --exclude='bin/' --exclude='obj/' --exclude='*.db' --exclude='*.db-shm' --exclude='*.db-wal' /mnt/c/Users/<utilisateur-windows>/source/repos/esport-team-manager/ ~/source/repos/esport-team-manager/
cd ~/source/repos/esport-team-manager
dotnet test RepriseWeb.slnx
```

La copie Linux conserve son propre dossier `.git` et sa base SQLite locale. L’option `--delete` aligne les fichiers de travail sans supprimer ces éléments ni modifier le dépôt Windows.

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

## Point de clôture ACC-006

ACC-006 est fusionné par la PR #30 et validé par 141 tests. La CI master #80 et le déploiement manuel #5 sont réussis. La migration `20260825201423_AddPendingEmailReservationConstraints` est appliquée en production.
