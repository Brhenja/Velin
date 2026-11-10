# Velin — estado del desarrollo (traspaso a otro chat)

Launcher de Minecraft Java para Windows. C# / .NET 8 / WinForms, repo https://github.com/Brhenja/Velin (rama `main`).
Última versión PUBLICADA: **1.1.1**. **1.2.0** está terminada: compila, el usuario la probó (fuentes, splash, ícono, música,
primer inicio, filtros, importadores, acceso directo y aviso de actualización andan bien) y falta solo publicarla a mano
desde la página de Releases (ver docs/COMPILAR.md → "Publicar una release" y docs/RELEASE-1.2.0.md).

## Reglas de trabajo del usuario
- Preguntarle las dudas ANTES de tocar código. Idioma: español rioplatense. Filosofía: liviano, minimalista, sin dependencias nuevas, sin regresiones.
- Entrega: .zip con el proyecto completo + README/CHANGELOG/notas de release actualizados + comandos de PowerShell (build.ps1, git, gh).
- Reparto acordado: 1.2.0 comodidad e infraestructura · 1.3.0 calidad · 1.4.0 separar `Velin.Core` · 2.0 frontend Avalonia/Linux (dual boot antiX 19; apuntar a distros comunes; sin música en Linux al inicio).

## Estructura (Velin/Velin/)
- `Services/` lógica pura (sin WinForms; el proyecto de pruebas `tests/ServiceTests` compila Services+Models, no UI/Forms). `Models/`, `Forms/`, `UI/` (Theme, Localization, SkinRenderer, ImageCache, ContentTextRenderer...).
- Datos del usuario en `data\` junto al .exe. Temas JSON en `Assets\Themes`, idiomas JSON en `Assets\Lang` (+ `data\lang` del usuario).
- Pruebas: `dotnet run --project tests\ServiceTests -c Release` (script propio con `Check(bool, nombre)`; devuelve código 1 si algo falla).
- Build: `.\build.ps1` (pruebas + publish + zip portable + instalador Inno). Flags: `-SkipTests -SkipInstaller -NoSingleFile`.

## Qué trae 1.1.0 y 1.1.1 (ya publicadas)
Login Microsoft (Client ID aprobado c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb), detalles de mods (RichTextBox nativo, galería, visor), Configuración por secciones,
temas Black & White, Java opcionales (GraalVM 21 / Semeru OpenJ9), paginación en buscadores, gestor de skins (premium + biblioteca local + CustomSkinLoader para offline),
cara de la skin en la ventana principal.

## 1.2.0 — HECHO en este zip (sin compilar)
1. **Empaquetado**: publish en UN solo `Velin.exe` (`-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`, sin compresión) + carpeta `Assets\`; `build.ps1 -NoSingleFile` vuelve al modo viejo.
   Zip portable ahora trae UNA carpeta `Velin\`. Instalador Inno: `[InstallDelete]` borra las .dll sueltas de versiones viejas. `SHA256SUMS.txt` en `dist\`.
2. **GitHub Actions**: `.github/workflows/ci.yml` (pruebas+build en push/PR) y `release.yml` (al subir tag `vX.Y.Z`: pruebas, build, Inno vía choco, SHA256SUMS, notas desde CHANGELOG, release; paso opcional de firma SignPath si existe el secreto `SIGNPATH_API_TOKEN` y las variables `SIGNPATH_ORGANIZATION_ID/PROJECT_SLUG/POLICY_SLUG`). Verifica que el tag coincida con `<Version>` del csproj.
3. **Aviso de actualización**: `Services/UpdateService.cs` (API de releases de GitHub, hash por `digest` o SHA256SUMS.txt, solo descarga de github.com/Brhenja/Velin/releases/download), `Forms/UpdateForm.cs` (notas, "Descargar último .exe" que baja el instalador, verifica y lo ejecuta cerrando Velin; en portable abre la página), chequeo al iniciar cada 12 h (`CheckUpdatesOnStart`, `LastUpdateCheckUtc`, `SkippedUpdateVersion` en AppSettings), botón manual en Configuración → Acerca de.
4. **Acceso directo / --jugar**: `Services/ShortcutService.cs` (.lnk vía WScript.Shell COM dinámico), `Program.Main(string[] args)` → `MainForm(autoPlayInstance)`; ítem "Crear acceso directo..." en el menú de la instancia.
5. **Primer inicio guiado**: `Forms/FirstRunForm.cs` (idioma detectado por cultura, tema, usuario, avisos) cuando no existe settings.json (`SettingsManager.Exists`).
6. **Sistema de idiomas por JSON**: `Services/LanguageCatalog.cs` + `UI/Localization.cs` (carga `Assets\Lang\<código>.json` + `data\lang`, fallback a inglés, luego clave). Fuente normal para idiomas fuera de {es,en,pt-BR,de,fr,it} (`Theme.UseMedieval`). Selector de idioma de Configuración dinámico. Generados: `es.json` y `en.json` (414 claves; clave `lang.name` = nombre del idioma).
7. **Filtros del buscador**: `Forms/ContentFilterBar.cs` (orden, categoría, versión de MC editable, loader), `ContentFilter/ContentSort/ContentCategory` en Models, `ContentCatalog` (categorías Modrinth/CurseForge y versiones), `SearchPageAsync(..., filter)`.
8. **Actualizar modpacks (solo Modrinth)**: Instance guarda `ModpackSource/ProjectId/VersionId/VersionName` al instalar; `modpack-files.json` por instancia; `Services/ModpackUpdater.cs` (aparta a `modpack-update-trash\` lo que el pack nuevo ya no trae, baja solo lo que cambió por SHA1, overrides sin pisar config existente, `InstanceManager.ChangeVersion`), `Forms/ModpackUpdateForm.cs`, ítem "Actualizar modpack..." en el menú. CurseForge: se registra el origen pero NO hay actualización todavía.
9. **Importadores**: `Services/LauncherImporter.cs` (Prism/MultiMC/PolyMC, app de CurseForge, launcher oficial `.minecraft`; copia archivos, no toca el original; opciones mundos/mods/otros) + `Forms/ImportLaunchersForm.cs`; el botón "Importar..." ahora abre un menú (archivo / otro launcher).
10. Pruebas nuevas en `tests/ServiceTests/Program.cs` (bloque "v1.2.0"), incluida una que valida TODOS los `Assets\Lang\*.json` (mismas claves que es.json, mismos {0}/{1}, sin vacíos, con `lang.name`).
11. Bug corregido durante el desarrollo: choque de claves `btn_check_updates` (botón de mods de la ventana principal) → el botón de Configuración usa `btn_check_launcher_updates`.

## 1.2.0 — cerrada (lo hecho después de la lista de arriba)
- Compila sin errores ni warnings. Traducciones creadas: pt-BR, ru, de, fr, it, zh-CN, ja, tr, pl, uk, ko (13 idiomas; la prueba exige >= 13).
  No las revisó ningún nativo.
- Bug de empaquetado: el publish en un solo archivo NO copiaba a `publish\Assets` las .ttf, `velin.ico` ni `velin_bgmusic.wav`. `build.ps1` ahora copia
  `Velin\Assets` completa después del publish y falla si falta algo imprescindible. Las fuentes siguen como archivos sueltos (el launcher debe consumir poco RAM:
  NO cargarlas desde memoria).
- Fuentes por idioma: es/en/pt-BR/de/fr/it usan las medievales; el resto la normal de Windows; ko/ja/zh usan Malgun Gothic / Yu Gothic UI / Microsoft YaHei UI
  (`LanguageCatalog.CjkFontNames`, `Theme.PlainFont`).
- Primer inicio: ahora también splash, música y click; la barra de título se repinta al cambiar de tema (`RefreshTheme`).
- `release.yml` es manual (workflow_dispatch) y solo deja artefactos; las releases se suben a mano desde la web. SignPath NO fue aprobado (el proyecto todavía no
  tiene la relevancia que piden): el instalador sigue sin firmar.
- Documentación al día: README, README.en, CHANGELOG, docs/COMPILAR.md y docs/RELEASE-1.2.0.md.

## 1.3.0 (después)
Escalado HiDPI (ventanas de tamaño fijo; ya hay PerMonitorV2 en Program.cs), descargas en paralelo de librerías/assets (revisar `MinecraftDownloader`), analizador de crashes, capas (capes) e historial de skins (API Mojang: GET /minecraft/profile, PUT /minecraft/profile/capes/active, DELETE ...), firma del instalador (reintentar SignPath cuando el proyecto tenga más relevancia, o certificado de pago), actualización de modpacks de CurseForge.

## Datos técnicos útiles
- Mojang skins: GET `https://api.minecraftservices.com/minecraft/profile`, POST `.../minecraft/profile/skins` (multipart variant+file), DELETE `.../skins/active`; públicas por UUID en sessionserver (cache 30 min en `data\cache\skins`).
- Modrinth: `/v2/search` (index, offset, total_hits), `/v2/project/{id}` (raw_icon_url, gallery.raw_url), `/v2/tag/category`. CurseForge: `/v1/mods/search` (sortField 2 popularidad, 3 actualizado, 6 descargas, 11 lanzamiento; index+pageSize ≤ 10000), `/v1/categories`.
- Offline + skins: se copia a `<instancia>\minecraft\CustomSkinLoader\LocalSkin\skins\<usuario>.png` (el mod hay que instalarlo aparte).
- WebP/SVG: GDI+ no los decodifica; `ImageCache` prueba la URL alternativa y luego muestra letra/nada.
