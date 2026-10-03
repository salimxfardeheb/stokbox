# Recette manuelle de Stokbox

Liste de contrôle à dérouler avant chaque livraison, sur chacune des machines ci-dessous.
Cocher chaque point ; noter la version testée, la machine et tout écart en bas du document.

## Machines

| Machine | Rôle |
| --- | --- |
| Windows 7 SP1 **32 bits**, machine virtuelle propre (sans .NET Framework 4.8) | cible minimale, architecture x86 |
| Windows 7 SP1 **64 bits**, machine virtuelle propre (sans .NET Framework 4.8) | cible minimale, architecture x64 |
| Windows 10 ou 11 (64 bits) | poste courant, .NET Framework 4.8 déjà présent |

« Propre » : Windows installé, Service Pack 1, aucune version de Stokbox, aucun dossier `C:\ProgramData\Stokbox`.
Prendre un instantané de la machine virtuelle avant de commencer, pour pouvoir rejouer l'installation.

## Matériel et fichiers

- [ ] `Stokbox-Setup-<version>.exe` produit par `build-release.ps1` **sans** l'option `-WithoutDotNetRedist`.
- [ ] Pour la mise à jour (section 11) : un second installateur de version supérieure. Le produire en augmentant
      `<Version>` dans `Directory.Build.props` (par exemple 1.0.0 → 1.0.1) puis en relançant `build-release.ps1`.
- [ ] Une imprimante d'étiquettes thermique 203 dpi avec son pilote Windows, chargée en étiquettes 50 × 30 mm.
- [ ] Une imprimante ticket 80 mm avec son pilote Windows.
- [ ] Un lecteur de code-barres USB (mode clavier, suffixe Entrée).
- [ ] Une clé USB.

À défaut de matériel sur une machine virtuelle, dérouler les sections 5 et 7 sur un poste réel et le noter.

---

## 1. Installation

- [ ] Lancer l'installateur avec un compte administrateur : l'assistant est en français et porte l'icône Stokbox.
- [ ] **Windows 7 uniquement** : .NET Framework 4.8 manque, son installation démarre toute seule (fenêtre Microsoft,
      plusieurs minutes), sans connexion internet.
  - [ ] Si elle est refusée (message « conditions non remplies »), installer les mises à jour Windows
        (prise en charge SHA-2 : KB4474419 et KB4490628 ; D3DCompiler : KB4019990), relancer l'installateur et
        noter les mises à jour qui ont été nécessaires.
  - [ ] Si un redémarrage est demandé à la fin, l'accepter ; Stokbox est installé au retour.
- [ ] **Windows 10/11** : aucune installation de .NET n'est proposée.
- [ ] Le dossier proposé est `C:\Program Files\Stokbox` (y compris sur Windows 64 bits).
- [ ] La case « Créer un raccourci sur le Bureau » est cochée par défaut.
- [ ] Après l'installation : raccourci sur le Bureau et dans le menu Démarrer, tous deux avec l'icône Stokbox.
- [ ] `C:\Program Files\Stokbox` contient `Stokbox.exe` et les deux dossiers `x86` et `x64`.
- [ ] Stokbox figure dans « Programmes et fonctionnalités » avec le bon numéro de version.

## 2. Premier lancement

- [ ] Stokbox démarre depuis le raccourci, sans message d'erreur, en quelques secondes.
- [ ] L'écran « Premier lancement » s'affiche (couleurs : fond clair, bouton principal rouge).
- [ ] Valider le formulaire vide : un message rouge apparaît sous le nom de la boutique et sous le mot de passe.
- [ ] Un mot de passe de 7 caractères est refusé ; une confirmation différente est refusée.
- [ ] Saisir la boutique (nom, adresse, téléphone) et un mot de passe de 8 caractères ou plus : l'écran Vente s'ouvre.
- [ ] Le menu est bleu marine, l'entrée sélectionnée est soulignée de rouge, les textes sont lisibles (police Segoe UI).
- [ ] Fermer puis relancer : l'écran de connexion s'affiche avec le logo.
- [ ] Un mauvais mot de passe est refusé (« Mot de passe incorrect. ») ; le bon ouvre l'application.
- [ ] Paramètres > À propos affiche le numéro de version de l'installateur.
- [ ] La fenêtre et la barre des tâches portent l'icône Stokbox.

## 3. Produit

- [ ] Produits > Nouveau sans catégorie : la fenêtre des catégories s'ouvre ; créer « Épicerie » et « Boissons ».
- [ ] Créer un produit avec le prix `1250,5` : il apparaît avec un code-barres de 13 chiffres commençant par `20`
      et le prix `1 250,50 DA`.
- [ ] Créer un produit avec le prix `35.00` (point) : accepté, affiché `35,00 DA`.
- [ ] Un prix de vente à `0` et une désignation vide sont refusés, avec le message sous le champ.
- [ ] Rechercher `cafe` trouve « Café » (accents et majuscules ignorés).
- [ ] Modifier un produit : le code-barres ne peut pas être changé.
- [ ] Archiver un produit : il disparaît de la liste, réapparaît avec « Afficher archivés ».
- [ ] Supprimer une catégorie qui contient un produit est refusé.

## 4. Entrée de stock

- [ ] Entrées de stock : le curseur est dans le champ de recherche dès l'ouverture.
- [ ] Taper le code-barres d'un produit puis Entrée : le produit est sélectionné, le curseur passe sur la quantité.
- [ ] Saisir `10` puis Entrée : l'entrée apparaît dans la liste de la session, le curseur revient à la recherche.
- [ ] Taper quelques lettres de la désignation : la liste de résultats s'ouvre ; flèches puis Entrée sélectionnent.
- [ ] Une quantité `0`, vide ou `abc` est refusée.
- [ ] Un produit archivé ne peut pas recevoir d'entrée.
- [ ] Produits : la colonne Stock affiche bien la somme des entrées.

## 5. Étiquette scannée

- [ ] Dans les préférences Windows de l'imprimante d'étiquettes, régler le papier sur 50 × 30 mm.
- [ ] Paramètres > Étiquettes : 50 × 30, 1 colonne × 1 ligne, choisir l'imprimante, Enregistrer.
- [ ] « Imprimer une étiquette de test » : une étiquette sort **sans boîte de dialogue**, rien n'est coupé.
- [ ] Étiquettes : cocher un produit, 2 exemplaires, l'aperçu montre l'étiquette aux bonnes proportions ; Imprimer : 2 étiquettes.
- [ ] L'étiquette porte la désignation (2 lignes au plus), le prix en gros (`1 250,50 DA`), le code-barres et son numéro.
- [ ] Entrées de stock : scanner l'étiquette imprimée → le **bon produit** est sélectionné.
- [ ] Après une entrée de stock, « Imprimer N étiquettes ? » (ou F6) imprime N étiquettes.
- [ ] Sans imprimante enregistrée, la boîte d'impression Windows s'affiche.

## 6. Vente

- [ ] Vente est l'écran d'accueil ; le curseur est dans la recherche.
- [ ] Scanner trois articles différents : trois lignes, total correct, sans toucher la souris.
- [ ] Scanner deux fois le même article : une seule ligne, quantité 2.
- [ ] `+` et `-` changent la quantité de la ligne sélectionnée ; ↑ ↓ changent de ligne ; Suppr retire la ligne.
- [ ] Scanner un article au-delà de son stock : « Stock insuffisant : X disponible(s) ».
- [ ] Échap propose de vider le panier ; refuser le conserve.
- [ ] F2 ouvre l'encaissement, montant pré-rempli avec le total ; taper un montant supérieur : la monnaie s'affiche.
- [ ] Un montant inférieur au total est refusé.
- [ ] Entrée valide : le panier se vide, le bandeau vert rappelle le numéro `V-AAAAMMJJ-0001` et la monnaie.
- [ ] Chronométrer : 3 articles scannés, F2, Entrée → moins de 10 secondes, sans souris.
- [ ] Produits : le stock des articles vendus a diminué.

## 7. Ticket

- [ ] Paramètres > Ticket : choisir l'imprimante ticket, Enregistrer ; « Imprimer un ticket de test » sort un ticket.
- [ ] Le ticket porte le nom, l'adresse et le téléphone de la boutique ; rien n'est coupé à droite ;
      le papier sort assez pour être découpé.
- [ ] Après une vente : « Imprimer le ticket ? » ; Entrée imprime, Échap n'imprime pas.
- [ ] Le ticket d'une vente porte le numéro, la date et l'heure, les lignes (quantité × prix, total), le total,
      le reçu, le rendu et « Merci de votre visite ».
- [ ] Imprimante ticket sur « Aucune » : le ticket n'est plus proposé après une vente.

## 8. Retour et annulation

- [ ] Historique : la vente du jour est listée (numéro, date et heure, articles, total, statut « Validée »).
- [ ] Sélectionner la vente : ses lignes s'affichent.
- [ ] Retour d'articles : retourner 1 article ; le montant à rembourser est le prix payé ; le stock remonte de 1.
- [ ] Un second retour ne propose que la quantité restante ; une quantité supérieure est refusée.
- [ ] « Annuler la vente » est grisé pour une vente qui a un retour.
- [ ] Sur une autre vente : Annuler la vente demande confirmation ; après confirmation, statut « Annulée »,
      stock revenu, boutons Retour et Annuler grisés.
- [ ] Rechercher une vente par son numéro la retrouve quelle que soit la période affichée.
- [ ] Réimprimer le ticket fonctionne ; celui d'une vente annulée porte « VENTE ANNULÉE ».

## 9. Sauvegarde

- [ ] Fermer Stokbox : un fichier `stokbox_AAAAMMJJ_HHMMSS.db` apparaît dans `C:\ProgramData\Stokbox\backups`.
- [ ] Ouvrir et fermer Stokbox 8 fois : il ne reste que 7 fichiers, les plus récents.
- [ ] Paramètres > Sauvegarde > « Sauvegarder maintenant… » vers la clé USB : le message donne le nom du fichier,
      le fichier est sur la clé, seul (pas de fichier `-wal` à côté).
- [ ] Choisir un dossier interdit en écriture : un message d'échec clair, aucun fichier partiel.

## 10. Restauration

- [ ] Après la sauvegarde sur clé USB : créer un produit « TEMOIN » et faire une vente.
- [ ] Paramètres > Sauvegarde > « Restaurer une sauvegarde… » : choisir le fichier de la clé ; une confirmation
      explique que tout sera remplacé et que la base actuelle est sauvegardée avant.
- [ ] Confirmer : Stokbox redémarre et demande le mot de passe.
- [ ] Le produit « TEMOIN » et la vente n'existent plus ; tout le reste est comme au moment de la sauvegarde.
- [ ] Une sauvegarde de la base remplacée a été ajoutée dans `C:\ProgramData\Stokbox\backups`.
- [ ] Restaurer un fichier quelconque renommé en `.db` (une photo, par exemple) est refusé, rien n'est modifié.
- [ ] Changer le mot de passe (Paramètres > Sécurité, ancien mot de passe exigé), fermer, rouvrir : le nouveau est demandé.

## 11. Mise à jour par-dessus

- [ ] Noter le nombre de produits, le stock d'un produit et le numéro de la dernière vente.
- [ ] Laisser Stokbox ouvert et lancer l'installateur de la version supérieure : il propose de fermer Stokbox.
- [ ] L'installation se fait dans le même dossier, sans redemander .NET.
- [ ] Au lancement : l'écran de **connexion** s'affiche (pas « Premier lancement »), le mot de passe est inchangé.
- [ ] Produits, stock, ventes, paramètres d'étiquette, imprimantes et boutique sont inchangés.
- [ ] Paramètres > À propos affiche la nouvelle version ; « Programmes et fonctionnalités » n'a qu'une seule entrée Stokbox.
- [ ] Une vente et une impression d'étiquette fonctionnent après la mise à jour.

## 12. Désinstallation

- [ ] Désinstaller depuis « Programmes et fonctionnalités ».
- [ ] Un message indique que les données sont conservées dans `C:\ProgramData\Stokbox`.
- [ ] `C:\Program Files\Stokbox` et les raccourcis ont disparu.
- [ ] `C:\ProgramData\Stokbox` contient toujours `stokbox.db` et le dossier `backups`.
- [ ] Réinstaller : l'écran de connexion s'affiche et toutes les données sont là.

## 13. Points propres à chaque machine

- [ ] **Windows 7 32 bits** : toutes les sections passent (l'application utilise `x86\SQLite.Interop.dll`).
- [ ] **Windows 7 64 bits** et **Windows 10/11** : toutes les sections passent (`x64\SQLite.Interop.dll`).
- [ ] Avec un second compte Windows **non administrateur** : Stokbox démarre, une vente s'enregistre
      (le dossier des données est modifiable par tous les comptes du poste).
- [ ] Aucun fichier n'a été créé dans `C:\ProgramData\Stokbox\logs` pendant la recette ; sinon, joindre le journal.
- [ ] Couper le réseau de la machine : Stokbox fonctionne à l'identique.

---

## Résultat

| Version testée | Machine | Date | Testeur | Résultat | Écarts constatés |
| --- | --- | --- | --- | --- | --- |
|  | Windows 7 SP1 32 bits |  |  |  |  |
|  | Windows 7 SP1 64 bits |  |  |  |  |
|  | Windows 10 / 11 |  |  |  |  |
