# Stokbox

Logiciel Windows mono-poste de gestion de stock, vente au comptoir et impression d'étiquettes code-barres de prix, pour boutiques de tout secteur. Hors ligne. Interface en français. Devise : dinar algérien (DA).

## Stack imposée (ne pas changer)

- C# 7.3, WPF, .NET Framework 4.8 (cible minimale : Windows 7 SP1 ; doit aussi tourner sur Windows 10 et 11).
- Projets au format SDK (`<TargetFramework>net48</TargetFramework>`, `<UseWPF>true</UseWPF>`).
- Base : SQLite, un seul fichier, via `System.Data.SQLite.Core`.
- Accès données : Dapper. Pas d'Entity Framework.
- MVVM : CommunityToolkit.Mvvm.
- Code-barres : ZXing.Net (rendu EAN-13 en image).
- Impression : API d'impression WPF (`PrintDialog`, `FixedDocument`) via le pilote Windows. Pas de ZPL/TSPL.
- Tests : xUnit, cible net48.
- Installateur : Inno Setup 6.

Interdit : .NET Core / .NET 5+, Electron, WebView2, toute dépendance qui ne supporte pas net48 ou Windows 7, tout appel réseau.

## Architecture

- `src/Stokbox.Core` : entités, règles métier, interfaces de dépôts et de services. Aucune référence à WPF ni à SQLite.
- `src/Stokbox.Data` : implémentation SQLite + Dapper, migrations.
- `src/Stokbox.App` : WPF (vues, ViewModels, impression, injection de dépendances).
- `tests/Stokbox.Core.Tests`, `tests/Stokbox.Data.Tests` (base SQLite temporaire par test).
- `tests/Stokbox.App.Tests` : rendu et mise en page des étiquettes (éléments WPF créés sur un thread STA).
- `installer/` : script Inno Setup.

## Règles de gestion (obligatoires)

- RG-01 Toute variation de stock passe par un mouvement enregistré (ENTREE, VENTE, RETOUR, ANNULATION). La quantité en stock = somme des mouvements ; jamais d'UPDATE direct de quantité.
- RG-02 Code-barres interne unique, jamais réattribué, même après archivage.
- RG-03 Un produit ayant un historique ne se supprime pas, il s'archive. Un produit archivé ne se vend pas.
- RG-04 Chaque ligne de vente fige le prix d'achat et le prix de vente au moment de la vente.
- RG-05 Une vente validée n'est jamais modifiée ni supprimée ; seulement annulée ou retournée.
- RG-06 Quantité retournée ≤ quantité vendue − retours déjà faits.
- RG-07 Validation de vente + mouvements de stock dans une seule transaction.
- RG-08 Montant reçu en espèces ≥ total pour valider.
- RG-09 Prix en DA, 2 décimales max, stockés en entier de centimes (INTEGER). Prix de vente > 0.
- RG-10 Vente bloquée si la quantité demandée dépasse le stock disponible.

## Conventions

- Code et identifiants en anglais ; textes de l'interface et messages d'erreur en français.
- Dates stockées en UTC ISO-8601, affichées en heure locale.
- Toute requête SQL est paramétrée.
- Chaque changement de schéma = nouveau script de migration numéroté ; ne jamais modifier une migration déjà livrée.
- Les ViewModels ne touchent jamais SQLite directement : ils passent par les services de Core.
- Avant de déclarer une tâche terminée : `dotnet build` sans avertissement nouveau et `dotnet test` vert.

## Hors périmètre v1 (ne pas implémenter)

Multi-postes, codes EAN fabricant, comptes multiples, fournisseurs, crédit client, remises, TVA/facturation, variantes, lots, péremption, numéros de série, alertes de stock, rapports, licence/activation, autres langues.
