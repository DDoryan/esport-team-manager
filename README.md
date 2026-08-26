# Esport Team Manager

Application web de gestion d’équipes esport : comptes confirmés par courriel, équipes, calendrier partagé et activités planifiées.

## État du projet

- **Jalon atteint :** P0 terminé le 23 août 2026.
- **Version de référence :** release GitHub de préversion `v0.1.0-p0`, publiée sur le commit vérifié `ab326f6` après la clôture documentaire.
- **Production :** <https://esport-team-manager-production.up.railway.app>
- **Dernier ticket clôturé :** TEAM-003 le 26 août 2026, en 4 h pour 4 h estimées.
- **Avancement :** 26 éléments terminés sur 60 et 172 h estimées restantes.
- **Validation :** 144 tests automatisés réussis localement sous WSL2 et dans GitHub Actions, Smart App Control maintenu actif, CI de `master` #84 verte et production Railway validée après le déploiement manuel #6.
- **Prochain ticket :** TEAM-009 — Naviguer entre ses équipes.
- **Projection :** déficit maintenu à 32 h au 6 septembre et MVP complet projeté au 9 septembre 2026, sans réduire QLT-004, QLT-005, QLT-008 ni la recette.

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
- changement sécurisé d’adresse électronique après réauthentification, réservation unique d’une heure et confirmation à usage unique ;
- consultation sécurisée de la gestion d’équipe, membres actifs, historique d’appartenance conservé et navigation d’équipe partagée ;
- tableau des membres sur ordinateur et cartes responsive sur mobile.

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

Après TEAM-003 : six projets compilés sans erreur ; 144 tests automatisés réussis localement sous WSL2 et dans GitHub Actions.

### Exécution locale des tests avec Smart App Control

Sous Windows, Smart App Control peut empêcher `testhost.exe` de charger la bibliothèque non signée `SQLitePCLRaw.batteries_v2.dll`. Le chargement échoue alors avec le code `0x800711C7`, sans remettre en cause le code des tests ni leur résultat dans la CI Linux.

Smart App Control ne doit pas être désactivé. Les tests sont exécutés localement dans Ubuntu 24.04 sous WSL2, avec le SDK .NET 10.

Lors de la première utilisation, créer une copie Linux propre du dépôt en excluant les sorties générées sous Windows :

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0 rsync
mkdir -p ~/source/repos/esport-team-manager
rsync -a --exclude='.git/' --exclude='.vs/' --exclude='bin/' --exclude='obj/' --exclude='*.db' --exclude='*.db-shm' --exclude='*.db-wal' /mnt/c/Users/dorya/source/repos/esport-team-manager/ ~/source/repos/esport-team-manager/
cd ~/source/repos/esport-team-manager
dotnet test RepriseWeb.slnx
```

Avant les exécutions suivantes, resynchroniser la copie Linux depuis le dépôt Windows :

```bash
rsync -a --delete --exclude='.git/' --exclude='.vs/' --exclude='bin/' --exclude='obj/' --exclude='*.db' --exclude='*.db-shm' --exclude='*.db-wal' /mnt/c/Users/dorya/source/repos/esport-team-manager/ ~/source/repos/esport-team-manager/
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

## Point de clôture TEAM-003

TEAM-003 est fusionné par la PR #31 et validé par 144 tests. Les CI #82 et #84, le déploiement manuel #6, Railway, `/health` et la recette de production desktop/mobile sont réussis. Aucune migration n’était requise.

- consultation autorisée uniquement pour une appartenance active ;
- anciens membres refusés et périodes closes conservées sans onglet public ;
- réintégration projetée depuis la nouvelle période active ;
- identité, rôle, date d’entrée et statut Propriétaire affichés ;
- sélecteur d’équipe et onglets Calendrier / Gestion d’équipe dans l’en-tête partagé ;
- tableau desktop transformé en cartes mobiles sans débordement horizontal ;
- empreintes SHA-256 distante et locale de la sauvegarde identiques ;
- temps réel : 4 h pour 4 h estimées.

L’égalité des empreintes valide l’intégrité du transfert, pas une restauration. La restauration réelle de la base et d’une image reste planifiée dans QLT-008 après STR-004 et TEAM-002.