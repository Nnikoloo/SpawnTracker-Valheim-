# SpawnTracker (Valheim)

Mod de BepInEx que muestra dónde están los spawners de criaturas cercanos y cuánto falta para que reaparezcan.

## Uso
- **F8**: mostrar/ocultar.
- Etiquetas flotantes sobre cada spawner + lista de los más cercanos (lado derecho de la pantalla). Textos del juego en inglés.
- Colores: verde = vivo/lleno, amarillo = esperando, rojo = listo para aparecer, gris = inactivo.

Config: `BepInEx/config/nnikoloo.SpawnTracker.cfg` (se crea al primer arranque): tecla, rango (80 m), lista, etiquetas.

## Qué rastrea
| Tipo en el juego | Ejemplos | Qué muestra |
|---|---|---|
| `CreatureSpawner` | draugr/esqueletos en criptas, criaturas fijas de ubicaciones | "Alive" / "Respawns in m:ss" / "Ready" + qué lo bloquea (día/noche, distancia, base, progreso) |
| `SpawnArea` | nidos de greydwarf, pilas de huesos, cuerpos de draugr | Cuenta atrás al siguiente spawn y criaturas cerca/total vs máximo |

No incluye los spawns aleatorios del mundo (`SpawnSystem`), que no tienen un punto fijo.

## Limitaciones
- El temporizador usa el **tiempo del mundo**: dormir lo adelanta.
- En multijugador, el temporizador de los nidos solo se ve si tú eres el "dueño" de esa zona; los respawn de `CreatureSpawner` sí se ven siempre.
- `SpawnArea` avanza en pasos de 2 s, así que su cuenta atrás es aproximada.

## Compilar
```
.\package.ps1
```
Compila y crea `dist/SpawnTracker-<versión>.zip` listo para Thunderstore (importar en local o subir). Antes de cada release sube `version_number` en `thunderstore/manifest.json` y añade una entrada en `thunderstore/CHANGELOG.md`.

Para probar rápido sin zip: `dotnet build -c Release -p:DeployToProfile=true` copia el .dll directo al perfil `Default` (no lo combines con el mod importado, se cargaría dos veces). Rutas en `SpawnTracker.csproj` (`ValheimDir`, `BepInExDir`).

## Plan de modding (recon)
- Juego: `E:\SteamLibrary\steamapps\common\Valheim` (Steam 892970), Unity Mono, sin anti-cheat.
- Loader: BepInEx 5.4.23.5 (pack de Thunderstore), perfil `Default`. Logs: `<perfil>\BepInEx\LogOutput.log`.
- Ruta elegida: plugin BepInEx que **solo lee** el estado (ZDO + campos privados vía `AccessTools`), sin parches Harmony.
- Publicación: team `nnikoloo` en Thunderstore, GUID `nnikoloo.SpawnTracker`, repo https://github.com/Nnikoloo/SpawnTracker-Valheim-
