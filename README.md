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

