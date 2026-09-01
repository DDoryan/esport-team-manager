# Esport Team Manager

Application web de gestion d’équipes esport : comptes confirmés par courriel, équipes, calendrier partagé et activités planifiées.

## État du projet

- **Jalons atteints :** P0 terminé le 23 août 2026 ; socle Activités terminé le 30 août 2026.
- **Versions de référence :** préversion `v0.1.0-p0` sur `ab326f6`, puis préversion `v0.2.0-socle-activites` sur le commit vérifié `915cc1e`.
- **Production :** <https://esport-team-manager-production.up.railway.app>
- **Dernier ticket clôturé :** STR-006 — Associer stratégies et activités, le 1er septembre 2026, en 2 h pour 4 h estimées.
- **Avancement :** 45 éléments terminés sur 60 et 76 h estimées restantes.
- **Validation :** six projets compilés, audit réussi, 373 tests automatisés réussis sans échec ni test ignoré, PR #51 fusionnée, CI de branche #158 et CI `master` #160 réussies, sauvegarde PostgreSQL vérifiée par `pg_restore` et deux empreintes SHA-256 identiques, workflow de production #25, Railway `ACTIVE`, base déjà à jour, `/health` `Healthy` et recette authentifiée des associations validée en production.
- **Prochain ticket :** STR-007 — Supprimer une stratégie.
- **Projection :** journée du 31 août clôturée à 10 h et journée du 1er septembre maintenue en cours après 5 h, 67 h disponibles jusqu’au 6 septembre, déficit projeté de 9 h et MVP complet toujours projeté au 9 septembre 2026, sans réduire QLT-004, QLT-005 ni la recette.

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
- navigation sécurisée entre zéro, une ou plusieurs équipes, avec sélecteur partagé et dernière équipe accessible mémorisée dans un cookie minimal protégé ;
- invitation d’un compte confirmé par identité exacte `Pseudo#tag`, avec rôles contrôlés côté serveur, réponse d’échec neutre, limite persistée de 30 invitations par heure et par expéditeur, toutes équipes confondues ;
- index composite `Invitations(SenderUserId, CreatedAtUtc)` appliqué par migrations SQLite et PostgreSQL.
- panneau de notifications superposé et responsive pour accepter ou refuser les invitations et transferts sans quitter la page courante ;
- traitement privé des images PNG, JPEG et WebP par décodage réel, correction de l’orientation, suppression des métadonnées, redimensionnement maximal 512 × 512 et génération WebP ;
- stockage des images et miniatures sous des clés aléatoires, hors de `wwwroot`, avec protections contre la traversée de chemins et nettoyage compensatoire en cas d’échec de persistance.
- page Gestion d’équipe organisée en onglets Informations, Membres, Invitations et Propriété, sans pages de formulaire distinctes ;
- modification réservée au propriétaire du nom, du tag, du fuseau, de la description et du logo ;
- logo d’équipe PNG/JPEG/WebP de 2 Mo maximum et 64 × 64 pixels minimum, traité et remplacé par le stockage privé sécurisé ;
- interface des paramètres alignée à gauche, sans défilement vertical global sur ordinateur et sans débordement horizontal global sur mobile.
- sauvegarde quotidienne et manuelle de PostgreSQL et des stockages `team-logos`/`strategy-images` par GitHub Actions ;
- archives séparées chiffrées par AES-256-GCM, contrôlées par SHA-256 et conservées 30 jours ;
- restauration isolée validée sur PostgreSQL 18 avec 29 tables applicatives, 2 équipes, 5 migrations EF et 2 fichiers WebP privés.
- calendrier d’équipe enrichi par des filtres de type et d’état, des vues quatre semaines/semaine sur ordinateur et une liste mobile limitée au jour sélectionné ;
- type et état toujours identifiables par un libellé et une icône en complément de la couleur, avec débordement exact « + N autres ».
- recherche textuelle et filtres combinables par période, type, état, participant et résultat, contrôlés côté serveur dans une fenêtre maximale de 29 jours ;
- filtres conservés pendant la navigation interne, réinitialisation prévisible, activités annulées masquées par défaut et panneau responsive.
- référentiel contrôlé de treize cartes et liste privée des stratégies, isolée par équipe, filtrable et responsive ;
- création et modification d’une stratégie avec contenu minimal, URL HTTP/HTTPS et image PNG/JPEG/WebP stockée hors de `wwwroot` ;
- affichage privé des miniatures et images optimisées, aperçu intégré et téléchargement WebP avec un nom compréhensible ;
- association de plusieurs stratégies actives à une activité, conservation des associations historiques devenues inactives et refus côté serveur des identifiants étrangers, inactifs, vides ou répétés ;
- cartes d’association avec miniature privée ou nom de carte de remplacement, affichées sur trois, deux puis une colonne selon la largeur.

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
- `PrivateImageStorage__RootPath`, chemin absolu du stockage privé des images, hors de `wwwroot` et du dépôt source.

La chaîne PostgreSQL de production comprend `GSS Encryption Mode=Disable` : le service n’utilise pas Kerberos et conserve le chiffrement TLS sans tenter de charger les bibliothèques GSS absentes du conteneur.

Le service expose `/health` pour le contrôle de disponibilité. Le Dockerfile attendu par Railway se trouve à la racine : `/Dockerfile`.

Sur Railway, `UseForwardedHeaders()` traite uniquement `X-Forwarded-Proto`. `UseHttpsRedirection()` reste actif hors Railway mais est ignoré lorsque `RAILWAY_PROJECT_ID` est défini : Railway termine TLS et assure lui-même la redirection HTTP publique, tandis que ses requêtes internes ne doivent pas chercher un port HTTPS Kestrel.

Le déploiement automatique Railway est désactivé. La production est mise à jour depuis le workflow GitHub Actions `Déployer manuellement en production`, uniquement depuis `master`, après une CI verte et la création puis la vérification d’une sauvegarde PostgreSQL. Le workflow attend la fin réelle du déploiement Railway avant de contrôler `/health`.

La clôture d’un ticket déployé suit obligatoirement cet ordre : contrôles locaux, CI de la pull request, fusion, CI `master`, sauvegarde PostgreSQL distante téléchargée avec empreintes SHA-256 distante et locale identiques, workflow GitHub Actions manuel, déploiement Railway `ACTIVE`, recherche ciblée dans les journaux, healthcheck puis smoke test authentifié. Les empreintes de fichiers JavaScript ou CSS ne remplacent pas la vérification du dump PostgreSQL. Un déploiement lancé directement depuis l’interface Railway n’est pas la voie de production contrôlée.

## Sauvegarde de production

Le workflow GitHub Actions `Sauvegarder quotidiennement la production` s’exécute à **02 h 15 UTC** et peut aussi être déclenché manuellement. Il utilise l’environnement GitHub `production` et exige les secrets suivants :

- `RAILWAY_TOKEN` ;
- `BACKUP_ENCRYPTION_PASSWORD` ;
- `RAILWAY_SSH_PRIVATE_KEY_BASE64`.

Le workflow crée deux artefacts indépendants :

- `production-database-<identifiant UTC>` ;
- `production-private-images-<identifiant UTC>`.

Chaque artefact contient une archive `.enc` et son empreinte `.sha256`. Les archives sont chiffrées par `scripts/backup-encryption.mjs` avec AES-256-GCM et une clé dérivée par PBKDF2-SHA256 à 200 000 itérations. Un déchiffrement de contrôle doit reproduire exactement l’archive claire avant sa suppression et la publication de l’artefact.

La collecte des images parcourt récursivement uniquement `team-logos` et `strategy-images`, compare le nombre de fichiers distants et téléchargés et conserve des empreintes internes. Les catégories temporaires ne sont pas incluses.

La restauration de validation QLT-008 a été réalisée à partir des artefacts de l’exécution #8 dans un environnement isolé. Elle a confirmé 29 tables applicatives, 2 équipes, 5 migrations EF et 2 fichiers WebP privés. Une restauration de production doit toujours suivre la procédure d’incident et réappliquer les suppressions postérieures au point restauré.

## Compilation et tests

```powershell
dotnet build RepriseWeb.slnx
dotnet test EsportTeamManager.Tests/EsportTeamManager.Tests.csproj
```

Après ACT-010 : six projets compilés sans erreur ; 310 tests automatisés réussis localement et dans GitHub Actions, sans échec ni test ignoré. Le plan de recette couvre aussi les combinaisons de filtres, la navigation, la réinitialisation, le responsive et la chaîne contrôlée de livraison en production.

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