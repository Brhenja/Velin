# Velin 1.4.0

Ventana principal renovada, fuente a elección, Vault mejorada y sonido simplificado, más una reorganización interna del código.

## Novedades
- **Dos estilos de interfaz** (Ajustes → General): **Velin New** (por defecto), con botones **+ / − / engranaje** sobre la lista y botones con ícono (flecha para Jugar, rompecabezas para Contenido, carpeta, cofre para la Vault, engranaje para Ajustes y cabeza de Minecraft para Cuentas). **Mods** y **Modpacks** son botones propios. Y **Velin Legacy**, la disposición de siempre. El **+** permite crear una instancia, instalar **desde un modpack**, o importar.
- **Elegir una versión más vieja de un mod** (botón *Elegir versión...*, Modrinth y CurseForge): muestra las versiones del mod para la versión de Minecraft de la instancia; la elegida queda **fijada** y no se actualiza sola.
- **Mods instalados:** buscador y orden (A-Z, más recientes, más antiguos, activados / desactivados primero).
- **Doble clic sobre una instancia = Jugar.** El clic derecho suma *Abrir carpeta de la instancia* y *Revisar actualizaciones de mods*. Se quitó el botón "Más".
- **Fuente de texto** a elección en Ajustes, al lado del tema: Predeterminada, **Medieval** (la propia de Velin, aunque el tema no la use) o una lista corta de fuentes seguras.
- En español, "Configuración" pasó a llamarse **Ajustes**, y "Mods y contenido" pasó a **Contenido** (en todos los idiomas).
- Opción para **usar el Explorador de Windows** en vez del gestor de archivos de Velin.
- Idioma, fuente y estilo se aplican al reiniciar: Velin pregunta si querés reiniciar ahora.
- **Vault:** ventana más grande, renombrar (F2), atajos de teclado, resumen de la selección y confirmación antes de eliminar.
- **Sonido:** se quitó la música de fondo; queda el click, que ahora se puede repetir muchas veces seguidas.
- Internamente, la lógica pasó a un proyecto aparte, **`Velin.Core`**, sin WinForms (preparación para futuras interfaces).

## Notas
- Si compilás Velin desde el código: borrá las carpetas viejas `Velin\Models` y `Velin\Services` (ahora están en `Velin.Core\`). Ver `docs/COMPILAR.md`.
- El instalador **no está firmado**: Windows SmartScreen puede mostrar un aviso (*Más información → Ejecutar de todas formas*). Podés comprobar el archivo con `SHA256SUMS.txt`.
- Las traducciones (salvo español e inglés) no pasaron por hablantes nativos.

## Archivos
- `VelinSetup-1.4.0.exe`: instalador (sin permisos de administrador, en `%LocalAppData%\Velin`).
- `Velin-1.4.0-portable-win-x64.zip`: versión portable.
- `SHA256SUMS.txt`: hashes de los dos anteriores.

---

# Velin 1.4.0 (English)

A refreshed main window, a choice of text font, a better Vault and simpler sound, plus an internal reorganisation of the code.

## What's new
- **Two interface styles** (Settings → General): **Velin New** (default), with **+ / − / gear** buttons above the list and icon buttons (arrow for Play, puzzle piece for Content, folder, chest for the Vault, gear for Settings and a Minecraft head for Accounts). **Mods** and **Modpacks** are their own buttons. And **Velin Legacy**, the classic layout. The **+** lets you create an instance, install **from a modpack**, or import.
- **Double-click an instance to play.** The right-click menu now includes *Open instance folder* and *Check mod updates*. The "More" button is gone.
- **Choose an older version of a mod** (*Choose version...* button, Modrinth and CurseForge): lists the mod's versions for the instance's Minecraft version; the one you pick is **pinned** and will not update by itself.
- **Installed mods:** search box and sort order (A-Z, newest, oldest, enabled / disabled first).
- **Text font** choice in Settings, next to the theme: Default, **Medieval** (Velin's own, even if the theme does not use it) or a short list of safe fonts.
- "Mods and content" is now just **Content** (in every language).
- Option to **use Windows Explorer** instead of Velin's own file manager.
- Language, font and style apply on restart: Velin asks whether to restart now.
- **Vault:** bigger window, rename (F2), keyboard shortcuts, selection summary and a confirmation before deleting.
- **Sound:** background music removed; the click stays and can now repeat many times in a row.
- Internally, the logic moved to a separate project, **`Velin.Core`**, with no WinForms (groundwork for future interfaces).

## Notes
- If you build from source: delete the old `Velin\Models` and `Velin\Services` folders (they now live in `Velin.Core\`). See `docs/COMPILAR.md`.
- The installer is **not code-signed**: Windows SmartScreen may show a warning (*More info → Run anyway*). You can verify the file with `SHA256SUMS.txt`.
- Translations (except Spanish and English) have not been reviewed by native speakers.

## Files
- `VelinSetup-1.4.0.exe`: installer (no administrator rights, installs to `%LocalAppData%\Velin`).
- `Velin-1.4.0-portable-win-x64.zip`: portable build.
- `SHA256SUMS.txt`: hashes of the two files above.
