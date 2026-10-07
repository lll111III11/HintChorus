<!-- language tabs -->
**Idioma / 语言 / Language :** [中文（主）](../../README.md) ｜ [English](../en/README.md) ｜ [Français](../fr/README.md) ｜ [Русский](../ru/README.md) ｜ **Español** ｜ [多语言 / Multilingual](../languages.md)

---

# HintChorus — capa de aislamiento de interfaz para SCP:SL

**Permite que plugins que no se conocen entre sí compartan la misma pantalla sin que ninguno tenga que cambiar una sola línea de código.**

> El juego expone exactamente **un** canal de avisos («hint»), y gana el último que escribe.
> HintChorus convierte ese canal único en **ranuras independientes**, una por llamador, atribuidas
> automáticamente desde la pila de llamadas — un plugin que simplemente llama a `SendHint` / `SendBroadcast`
> funciona tal cual y deja de pisar a los demás.

[![License: CC0-1.0](https://img.shields.io/badge/License-CC0--1.0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)

## Descarga

**Con un solo archivo basta** : la DLL principal libera ella misma su cargador en el primer arranque.

| | |
| --- | --- |
| Descarga directa (el archivo lleva el prefijo `alpha-`) | [`alpha-HintChorus.dll`](../../alpha-HintChorus.dll) |
| Página de versiones (recomendado) | [Releases](https://github.com/lll111III11/HintChorus/releases/latest) |

El prefijo `alpha-` solo indica la fase de desarrollo : el **nombre de ensamblado sigue siendo `HintChorus`**,
así que renombrar el archivo no cambia nada de la carga.

Instalación :

1. Coloque `alpha-HintChorus.dll` en `plugins/global/`
2. Inicie el servidor (el primer arranque escribe `0HintChorus.Bootstrap.dll` y pide un reinicio — es deliberado)
3. Si lo necesita, edite `configs/<puerto>/HintChorus/config.yml` (**las claves van en snake_case** ; ver [`docs/default-config.yml`](../default-config.yml))

## Qué hace el plugin

1. **Atribución** : intercepta los seis puntos de entrada de la interfaz (hint / broadcast / consola / CASSIE /
   chat de administración / marcador de impacto) y recorre la pila de llamadas para identificar **qué plugin y qué punto de llamada** emite.
2. **UiId determinista** : `UUIDv5(superficie | ensamblado | punto de llamada | [desplazamiento IL])` — estable tras reiniciar.
3. **Una ranura por UiId** : fusión, orden estable por prioridad, emisión por un **escritor único**.
4. **Diseño estable** : ver abajo.

Ningún plugin necesita cambios de código ni referencias a este plugin.

## Diseño : por qué las posiciones dejan de saltar

El bloque de avisos está **anclado abajo** : cualquier variación de su altura desplaza todas sus líneas.
Hay dos modos disponibles (`layout_mode`) :

| Modo | Principio | Compromiso |
| --- | --- | --- |
| **`rows`** (por defecto) | número fijo de líneas para la zona volátil + una línea reservada por dueño, completada con líneas vacías | perfectamente estable, pero ocupa pantalla |
| **`offsets`** | sin líneas vacías : cada línea recibe un `<voffset>` que la fija en su propia ranura | igual de estable **y** sin ocupar pantalla |
| `compact` | no se rellena nada | lo más compacto, pero las posiciones varían |

`offsets` toma la **idea** de **RueI (CC0)** — « ni cuadrícula ni líneas : se calcula el desplazamiento » — con una
implementación propia ; la altura de las líneas se mide con la tabla de métricas embebida.
**No se incluye ningún código de terceros** : ver [`THIRD-PARTY.md`](../../THIRD-PARTY.md).

```yaml
layout_mode: rows        # cambie a offsets para ahorrar pantalla
offset_row_height: 40    # altura de una línea, en unidades voffset (pantalla completa ≈ 2140)
offset_sign: 1           # ponga -1 si el bloque se va en sentido contrario
offset_font_size: 20     # tamaño usado para medir el ancho del texto
```

## Posiciones : tres sintaxis en una

El juego solo da un canal de avisos, así que «posición» acaba siendo un `<voffset>` por línea.
HintChorus unifica las fuentes en una **rejilla de 3×3** (ancla) más una **escala vertical 0–1000** :

| Origen | Cómo se escribe | Interruptor |
| --- | --- | --- |
| **① Compatibilidad con lo existente** | el texto ya trae `<voffset>` / `<pos>` / `<align>` / `<line-height>` / `<indent>` / `<margin>` → **se deja tal cual**, ni se reordena ni se re-alinea | `honor_plugin_position_syntax` |
| **② Sintaxis propia · marcador** | `{{hc:top-right}}` o `{{hc:pos=750,align=left,offset=-90}}` — se elimina del texto, el jugador nunca lo ve | `enable_position_markers` |
| **③ Sintaxis propia · API C#** | `UiIsolation.SetHintPosition("MyPlugin", "pos=750,align=left")` — **la misma gramática** que el marcador | — |
| **④ Colocación automática** | si no hay nada de lo anterior, por nombre : `exp` / `level` / `经验` → centro y 90 más abajo ; `score` / `排行` → arriba-derecha ; `kill` / `击杀` → arriba-izquierda ; `timer` → arriba-centro ; `music` → abajo-derecha | `auto_layout_by_plugin_name` |

**Prioridad de resolución** : API explícita → marcador → etiquetas propias del plugin → tabla / nombre → por defecto (abajo-centro, apilado natural).

> **Especificación completa de la sintaxis propia** (tres dimensiones, gramática, alias, ejemplos, preguntas frecuentes)
> : [`docs/position-spec.es.txt`](../position-spec.es.txt).

## Compatibilidad

- Se admiten los dos estilos de llamada, **LabAPI** y **EXILED**, sin cambios en el lado del plugin.
- Superficies cubiertas : hint / broadcast / consola / CASSIE / chat de administración / marcador de impacto /
  ajustes del servidor (SSS) / pantalla del interfono.
- Carga con `LoadPriority.Highest` y un cargador con prefijo `0` : los parches se instalan **antes** que otros plugins.
- **Idiomas** : los avisos nativos del juego se traducen **según el idioma de cada jugador** — 5 idiomas de la ONU
  (`en` / `zh` / `fr` / `ru` / `es`) vienen integrados en la DLL ; los demás idiomas del cliente se cargan desde la
  carpeta `translations/` (los archivos oficiales están en [`docs/translations/`](../translations/)),
  con retroceso por línea al inglés.

> Conflicto conocido : un framework que parchea él mismo la salida de avisos se usa **en lugar de** este plugin —
> ambos necesitan el mismo canal único.

## Documentación

| Documento | Contenido |
| --- | --- |
| [`docs/languages.md`](../languages.md) | Todos los idiomas (22) y su estado |
| [`docs/position-spec.es.txt`](../position-spec.es.txt) | **Especificación de la sintaxis propia de posición v1** |
| [`docs/zh/`](../zh/) | Documentación china (principal) |
| [`docs/en/README.md`](../en/README.md) | English documentation |
| [`docs/default-config.yml`](../default-config.yml) | Configuración por defecto (66 claves) |
| [`CHANGELOG.md`](../../CHANGELOG.md) | Registro de cambios |

## Compilación

```bash
dotnet build src/HintChorus.csproj -c Release -p:ManagedDir="<su servidor>/SCPSL_Data/Managed"
```

## Licencia

**CC0-1.0** — dominio público, sin condiciones : ver [`LICENSE`](../../LICENSE).
Los contenidos de terceros quedan fuera de esta dedicación y se listan en [`THIRD-PARTY.md`](../../THIRD-PARTY.md).

## Proyectos relacionados

- [**RueI**](https://github.com/pawslee/RueI) — framework de avisos CC0 ; origen de la idea « calcular un desplazamiento »
- [**HintServiceMeow**](https://github.com/MeowServer/HintServiceMeow) — framework de avisos MIT, coloca cada aviso por coordenadas
- [**LabAPI**](https://github.com/northwood-studios/LabAPI) — la API de servidor sobre la que se apoya este proyecto
