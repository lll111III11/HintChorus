<!-- language tabs -->
**Langue / 语言 / Language :** [中文（主）](../../README.md) ｜ [English](../en/README.md) ｜ **Français** ｜ [Русский](../ru/README.md) ｜ [Español](../es/README.md) ｜ [多语言 / Multilingual](../languages.md)

---

# HintChorus — couche d'isolation d'interface pour SCP:SL

**Permet à des plugins qui ne se connaissent pas de partager le même écran sans qu'aucun n'ait à modifier son code.**

> Le jeu n'expose qu'**un seul** canal d'indication (« hint »), et le dernier qui écrit gagne.
> HintChorus transforme ce canal unique en **emplacements indépendants**, un par appelant, attribués
> automatiquement depuis la pile d'appels — un plugin qui appelle simplement `SendHint` / `SendBroadcast`
> fonctionne tel quel et cesse d'écraser les autres.

[![License: CC0-1.0](https://img.shields.io/badge/License-CC0--1.0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)

## Téléchargement

**Un seul fichier suffit** : la DLL principale libère elle-même son amorce au premier lancement.

| | |
| --- | --- |
| Téléchargement direct (le fichier porte le préfixe `alpha-`) | [`alpha-HintChorus.dll`](../../alpha-HintChorus.dll) |
| Page des versions (recommandé) | [Releases](https://github.com/lll111III11/HintChorus/releases/latest) |

Le préfixe `alpha-` n'indique que le stade de développement : le **nom d'assembly reste `HintChorus`**,
renommer le fichier ne change donc rien au chargement.

Installation :

1. Placez `alpha-HintChorus.dll` dans `plugins/global/`
2. Démarrez le serveur (le premier lancement écrit `0HintChorus.Bootstrap.dll` et demande un redémarrage — c'est voulu)
3. Modifiez si besoin `configs/<port>/HintChorus/config.yml` (**les clés sont en snake_case** ; voir [`docs/default-config.yml`](../default-config.yml))

## Ce que fait le plugin

1. **Attribution** : intercepte les six points d'entrée d'interface (hint / broadcast / console / CASSIE /
   chat administrateur / marqueur de touche) et remonte la pile d'appels pour identifier **quel plugin et quel site d'appel** émet.
2. **UiId déterministe** : `UUIDv5(surface | assembly | site d'appel | [décalage IL])` — stable après redémarrage.
3. **Un emplacement par UiId** : fusion, tri stable par priorité, émission par un **écrivain unique**.
4. **Mise en page stable** : voir ci-dessous.

Aucune modification de code n'est requise côté plugin, et aucune référence à ce plugin.

## Mise en page : pourquoi les positions ne bougent plus

Le bloc d'indications est **ancré en bas** : toute variation de sa hauteur décale toutes ses lignes.
Deux modes sont disponibles (`layout_mode`) :

| Mode | Principe | Compromis |
| --- | --- | --- |
| **`rows`** (défaut) | nombre de lignes fixe pour la zone volatile + une ligne réservée par émetteur, complétée par des lignes vides | parfaitement stable, mais occupe l'écran |
| **`offsets`** | aucune ligne vide : chaque ligne reçoit un `<voffset>` qui la fixe à son propre emplacement | aussi stable, **et** sans occuper l'écran |
| `compact` | rien n'est complété | le plus compact, mais les positions varient |

`offsets` reprend l'**idée** de **RueI (CC0)** — « ni grille ni lignes : on calcule le décalage » — avec une
implémentation propre, la hauteur des lignes étant mesurée via la table de métriques embarquée.
**Aucun code tiers n'est inclus** : voir [`THIRD-PARTY.md`](../../THIRD-PARTY.md).

```yaml
layout_mode: rows        # passez à offsets pour économiser l'écran
offset_row_height: 40    # hauteur d'une ligne, en unités voffset (écran complet ≈ 2140)
offset_sign: 1           # mettez -1 si le bloc part dans le mauvais sens
offset_font_size: 20     # taille utilisée pour mesurer la largeur du texte
```

## Positions : trois syntaxes en une

Le jeu ne donne qu'un canal d'indication ; « position » finit donc par être un `<voffset>` par ligne.
HintChorus unifie les sources sur une **grille d'ancrage 3×3** plus une **échelle verticale 0–1000** :

| Source | Comment l'écrire | Interrupteur |
| --- | --- | --- |
| **① Compatibilité avec l'existant** | le texte porte déjà `<voffset>` / `<pos>` / `<align>` / `<line-height>` / `<indent>` / `<margin>` → **laissé tel quel** (ni réordonné, ni réaligné) | `honor_plugin_position_syntax` |
| **② Syntaxe propre · marqueur** | `{{hc:top-right}}` ou `{{hc:pos=750,align=left,offset=-90}}` — retiré du texte, le joueur ne le voit jamais | `enable_position_markers` |
| **③ Syntaxe propre · API C#** | `UiIsolation.SetHintPosition("MyPlugin", "pos=750,align=left")` — **la même grammaire** que le marqueur | — |
| **④ Placement automatique** | si rien de tout cela, d'après le nom — `exp` / `level` / `经验` → milieu puis 90 plus bas ; `score` / `排行` → haut-droite ; `kill` / `击杀` → haut-gauche ; `timer` → haut-centre ; `music` → bas-droite | `auto_layout_by_plugin_name` |

**Priorité de résolution** : API explicite → marqueur → balises du plugin → table / nom → défaut (bas-centre, empilement naturel).

> **Spécification complète de la syntaxe propre** (trois dimensions, grammaire, alias, exemples, FAQ) :
> [`docs/position-spec.fr.txt`](../position-spec.fr.txt).

## Compatibilité

- Les deux styles d'appel **LabAPI** et **EXILED** sont pris en charge, sans modification côté plugin.
- Surfaces couvertes : hint / broadcast / console / CASSIE / chat administrateur / marqueur de touche /
  réglages serveur (SSS) / affichage de l'interphone.
- Chargement en `LoadPriority.Highest` avec une amorce préfixée `0` : les correctifs sont donc installés **avant** les autres plugins.
- **Langues** : les indications natives du jeu sont traduites **selon la langue de chaque joueur** —
  5 langues de l'ONU (`en` / `zh` / `fr` / `ru` / `es`) sont intégrées dans la DLL ; les autres langues du
  client se chargent depuis le dossier `translations/` (les fichiers officiels sont dans
  [`docs/translations/`](../translations/)), avec repli ligne par ligne sur l'anglais.

> Conflit connu : un framework qui corrige lui-même l'affichage des indications s'utilise **à la place** de ce
> plugin — les deux ont besoin du même canal unique.

## Documentation

| Document | Contenu |
| --- | --- |
| [`docs/languages.md`](../languages.md) | Toutes les langues (22) et leur état |
| [`docs/zh/`](../zh/) | Documentation chinoise (principale) |
| [`docs/en/README.md`](../en/README.md) | English documentation |
| [`docs/default-config.yml`](../default-config.yml) | Configuration par défaut (66 clés) |
| [`CHANGELOG.md`](../../CHANGELOG.md) | Journal des modifications |

## Compilation

```bash
dotnet build src/HintChorus.csproj -c Release -p:ManagedDir="<votre serveur>/SCPSL_Data/Managed"
```

## Licence

**CC0-1.0** — domaine public, sans condition : voir [`LICENSE`](../../LICENSE).
Les contenus tiers sont exclus de cette dédicace et listés dans [`THIRD-PARTY.md`](../../THIRD-PARTY.md).

## Projets liés

- [**RueI**](https://github.com/pawslee/RueI) — framework d'indications CC0 ; origine de l'idée « calculer un décalage »
- [**HintServiceMeow**](https://github.com/MeowServer/HintServiceMeow) — framework d'indications MIT, place chaque indication par coordonnées
- [**LabAPI**](https://github.com/northwood-studios/LabAPI) — l'API serveur sur laquelle ce projet repose
