<div align="center">

# Ringo Dead Cells Multiplayer

**Cooperativo en línea para dos en Dead Cells: recorred los biomas juntos, venced a los jefes juntos, cruzad la puerta juntos.**

[English](../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [日本語](README.ja.md) · [Русский](README.ru.md) · **Español**

</div>

Esta es una guía rápida. Los detalles, el funcionamiento interno y cómo compilarlo están en el [README en inglés](../README.md).

> [!WARNING]
> **Versión preliminar.** Todas las funciones superan pruebas automáticas con dos partidas en un mismo PC, pero el mod **todavía no se ha jugado entre dos cuentas reales de Steam**. Puede haber cierres, fallos de conexión o desincronización. Haz una copia de tu partida guardada antes de empezar.

## Funciones

- Cada jugador controla su propio héroe en la misma partida; enemigos, muertes y botín están sincronizados
- **Multijugador** en el menú principal abre la sala: salas de amigos y públicas, crear, unirse y gestionar
- Al pulsar una puerta esperas en ella y puedes cancelar cuando quieras; solo avanzáis juntos y por la misma puerta
- Si caes, tu compañero tiene 30 segundos para levantarte; mientras tanto la cámara lo sigue
- Ves el nombre de tu compañero sobre su héroe, y su atuendo si tú también lo tienes desbloqueado
- Los enemigos tienen más vida para dos; el equipo del suelo es compartido, y los pergaminos, células y oro son de cada uno
- Los niveles usan el progreso más bajo de los dos, y nunca se reescriben los desbloqueos de tu partida
- Únete cuando quieras, reconexión automática, y tras un cierre vuelves a tu nivel con tu equipo
- La sala y todos los mensajes siguen el idioma del juego (13 idiomas)

## Instalación

1. Ambos jugadores necesitan Dead Cells v35 en Steam y [Dead Cells Core Modding](https://github.com/dead-cells-core-modding/core) 35.14.2.
2. Descarga `RingoMP-v0.2.0.zip` de [Releases](https://github.com/Ringophilia/Ringo-DeadCell-MP/releases) y coloca la carpeta `RingoMP` en `coremod\mods\` dentro de la carpeta del juego.
3. Haz una copia de tu partida: `Steam\userdata\<tu id>\588650\remote`.
4. Inicia el juego con `coremod\core\host\startup\DeadCellsModding.exe`.

## Cómo jugar

1. Elige **Multijugador** en el menú principal.
2. Pulsa **`N`** para crear una sala e **`I`** para invitar a un amigo, o pulsa un número para unirte a una sala de la lista.
3. Cuando tu amigo esté dentro, el anfitrión pulsa **`S`** para una partida nueva o **`C`** para continuar. Tu amigo se une solo.

Otras teclas: `R` actualizar, `K` expulsar, `L` bloquear, `P` visibilidad, `X` cerrar o salir de la sala, `Esc` volver, `F5` abrir u ocultar la sala en cualquier momento.

## Limitaciones conocidas

- Dos jugadores como máximo, solo el juego principal: sin desafío diario ni Boss Rush.
- Quien vuelve recupera los objetos y el oro con los que entró en el nivel, pero no los modificadores de las armas.
- El banco y algunas salas secundarias aún no están adaptados.

Informa de errores en [Issues](https://github.com/Ringophilia/Ringo-DeadCell-MP/issues) y adjunta el registro más reciente de `coremod\logs`. Los problemas habituales están en [Troubleshooting](TROUBLESHOOTING.md).
