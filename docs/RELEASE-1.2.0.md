# Velin 1.2.0

Versión de comodidad e infraestructura: un solo `.exe`, avisos de actualización, accesos directos a instancias, 13 idiomas,
filtros en los buscadores, actualización de modpacks de Modrinth e importación desde otros launchers.

## Novedades
- **Un solo `Velin.exe`** (con la carpeta `Assets\` al lado). El portable es una sola carpeta `Velin\` y el instalador borra las `.dll` sueltas de versiones anteriores.
- **Aviso de actualización:** al iniciar y desde *Configuración → Acerca de*. *Descargar último .exe* baja el instalador, verifica el SHA256 y lo ejecuta. En el portable abre la página de descarga.
- **Acceso directo a una instancia** (`Velin.exe --jugar "nombre"`): menú de la instancia → *Crear acceso directo...*
- **Primer inicio guiado:** idioma, tema, usuario, avisos de actualización, pantalla de inicio, música y sonido de click.
- **13 idiomas:** español, inglés, portugués (Brasil), ruso, alemán, francés, italiano, chino simplificado, japonés, turco, polaco, ucraniano y coreano. Se pueden agregar los tuyos en `data\lang\`.
- **Filtros en los buscadores:** orden, categoría, versión de Minecraft y loader.
- **Actualizar modpacks de Modrinth** conservando mundos, opciones y mods propios.
- **Importar desde Prism / MultiMC / PolyMC, la app de CurseForge y el launcher oficial** (se copian los archivos, los originales no se tocan).
- **GitHub Actions** para compilar y probar en cada cambio.

## Correcciones
- El publish en un solo archivo dejaba afuera fuentes, ícono y música de `Assets\`; ahora el empaquetado los incluye y verifica.
- Al cambiar de tema en el primer inicio, la barra de título no cambiaba de color.
- Advertencias de compilación corregidas.

## Notas
- El instalador **no está firmado**: Windows SmartScreen puede mostrar un aviso (*Más información → Ejecutar de todas formas*). Podés comprobar el archivo con `SHA256SUMS.txt`.
- Las traducciones (salvo español e inglés) no pasaron por hablantes nativos: si encontrás un error, abrí un issue o mandá un pull request.
- Actualizar modpacks solo funciona con Modrinth; CurseForge todavía no.

## Archivos
- `VelinSetup-1.2.0.exe`: instalador (sin permisos de administrador, en `%LocalAppData%\Velin`).
- `Velin-1.2.0-portable-win-x64.zip`: versión portable.
- `SHA256SUMS.txt`: hashes de los dos anteriores.

---

# Velin 1.2.0 (English)

A convenience and infrastructure release: a single `.exe`, update notices, instance shortcuts, 13 languages, search filters,
Modrinth modpack updates and import from other launchers.

## What's new
- **A single `Velin.exe`** (with the `Assets\` folder next to it). The portable build is a single `Velin\` folder and the installer removes loose `.dll` files from old versions.
- **Update notice:** on startup and from *Settings → About*. *Download latest .exe* downloads the installer, checks its SHA256 and runs it. The portable build opens the download page.
- **Instance shortcut** (`Velin.exe --jugar "name"`): instance menu → *Create shortcut...*
- **Guided first run:** language, theme, username, update notices, startup screen, music and click sound.
- **13 languages:** Spanish, English, Portuguese (Brazil), Russian, German, French, Italian, Simplified Chinese, Japanese, Turkish, Polish, Ukrainian and Korean. You can add your own in `data\lang\`.
- **Search filters:** sort order, category, Minecraft version and loader.
- **Update Modrinth modpacks** keeping worlds, options and your own mods.
- **Import from Prism / MultiMC / PolyMC, the CurseForge app and the official launcher** (files are copied; originals are never touched).
- **GitHub Actions** to build and test on every change.

## Fixes
- Single-file publishing left some files out of `Assets\` (fonts, icon, music); packaging now includes and verifies them.
- Changing the theme in the first-run wizard did not recolor the title bar.
- Build warnings fixed.

## Notes
- The installer is **not code-signed**: Windows SmartScreen may show a warning (*More info → Run anyway*). You can verify the file with `SHA256SUMS.txt`.
- Translations (except Spanish and English) have not been reviewed by native speakers: if you find a mistake, please open an issue or send a pull request.
- Updating modpacks only works with Modrinth; CurseForge is not supported yet.

## Files
- `VelinSetup-1.2.0.exe`: installer (no administrator rights, installs to `%LocalAppData%\Velin`).
- `Velin-1.2.0-portable-win-x64.zip`: portable build.
- `SHA256SUMS.txt`: hashes of the two files above.
