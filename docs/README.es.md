<div align="center">

# Ringo Dead Cells Multiplayer

**Cooperativo en línea de hasta cuatro jugadores en Dead Cells: recorred los biomas juntos, venced a los jefes juntos, cruzad la puerta juntos.**

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [Русский](README.ru.md) · **Español**

</div>

Esta es una guía rápida. Los detalles, el funcionamiento interno y cómo compilarlo están en el [README en inglés](../README.md).

> [!WARNING]
> **Versión preliminar.** Todas las funciones superan pruebas automáticas con dos partidas en un mismo PC, pero el mod **todavía no se ha jugado entre dos cuentas reales de Steam**. Puede haber cierres, fallos de conexión o desincronización. Haz una copia de tu partida guardada antes de empezar.

## Funciones

- Cada jugador controla su propio héroe en la misma partida; enemigos, muertes y botín están sincronizados
- **Multijugador** en el menú principal abre la sala: salas de amigos y públicas, crear, unirse y gestionar
- Al pulsar una puerta esperas en ella y puedes cancelar cuando quieras; solo se avanza cuando todos esperan en la misma puerta
- Si caes, los demás tienen 30 segundos para levantarte; mientras tanto la cámara sigue a uno de ellos
- Ves el nombre de cada jugador sobre su héroe, y su atuendo si tú también lo tienes desbloqueado
- Los enemigos tienen más vida por cada jugador más; el equipo del suelo es compartido, y los pergaminos, células y oro son de cada uno
- Los niveles usan el progreso más bajo entre los jugadores, y nunca se reescriben los desbloqueos de tu partida
- Desafío diario y Boss Rush: el anfitrión empieza y los demás lo siguen; una partida jugada juntos no envía puntuación a la clasificación
- Únete cuando quieras, reconexión automática, y tras un cierre vuelves a tu nivel con tu equipo
- La sala y todos los mensajes siguen el idioma del juego (13 idiomas)

## Instalación

1. Cada jugador necesita Dead Cells v35 en Steam y [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core) 35.14.2.
2. Descarga `RingoMP-v0.4.0.zip` de [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases) y coloca la carpeta `RingoMP` en `coremod\mods\` dentro de la carpeta del juego.
3. Haz una copia de tu partida: `Steam\userdata\<tu id>\588650\remote`.
4. Inicia el juego con `coremod\core\host\startup\DeadCellsModding.exe`.

## Cómo jugar

1. Elige **Multijugador** en el menú principal.
2. Elige **Crear una sala** y luego **Invitar a un amigo**, o elige una sala de la lista para unirte.
3. Cuando tus amigos estén dentro, el anfitrión elige **Empezar una partida nueva**, **Continuar la partida guardada** o **Empezar el desafío diario**. Los demás se unen solos. Boss Rush empieza en su puerta dentro del juego, también para todos.

La sala funciona como las opciones del juego: haz clic con el ratón o muévete con el teclado o el mando como en cualquier otro menú. Durante la partida, el menú de pausa tiene **Multijugador**, que abre la misma sala. Las puertas de las salas secundarias (desafíos, el banco y similares) se cruzan juntos, igual que las salidas de nivel.

## Limitaciones conocidas

- Cuatro jugadores como máximo.
- Sin modo personalizado.

Informa de errores en [Issues](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues) y adjunta el registro más reciente de `coremod\logs`. Los problemas habituales están en [Troubleshooting](TROUBLESHOOTING.md).
