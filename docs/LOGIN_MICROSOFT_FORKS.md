# Login de Microsoft en Velin — guía para forks

> **Estado en Velin 1.0.0:** el login premium está **desactivado** en la versión oficial. El código del flujo
> está implementado, pero `Velin/Services/MicrosoftAuthConfig.cs` tiene el `ClientId` vacío, porque Mojang todavía
> no aprobó ninguna app de Velin. Con el `ClientId` vacío, la ventana **Cuentas** avisa que el login de Microsoft
> no está disponible y todo lo demás funciona con cuentas offline.
>
> Esta guía es para quien haga un **fork** y quiera activarlo con su propia app de Azure.

## Cómo funciona
Botón **Cuentas → Iniciar sesión con Microsoft**: se abre el navegador (OAuth con PKCE y redirección a
`http://localhost`), el usuario elige su cuenta con Minecraft Java y vuelve a Velin. Si el navegador falla hay un
botón "Usar un código en su lugar" (código de dispositivo). La sesión se guarda cifrada con DPAPI en
`data\accounts.json` y se renueva sola.

El **Client ID no es un secreto**: es el identificador público de la app, y todo launcher lo lleva adentro. Va escrito
en el código y los usuarios nunca lo cargan.

## Pasos (una sola vez)
1. Entrá a <https://portal.azure.com> con una cuenta Microsoft personal (puede pedirte crear un directorio
   gratuito; no se paga nada) → **Microsoft Entra ID → App registrations → New registration**.
2. **Name**: el nombre de tu launcher. **No puede contener** `Minecraft`, `Mojang`, `Microsoft`, `Xbox` ni `Live`.
3. **Supported account types**: *Personal Microsoft accounts only*.
4. **Redirect URI**: plataforma **Mobile and desktop applications** y valor `http://localhost`. Registrar.
5. En *Overview*, copiá el **Application (client) ID**.
6. En *Authentication → Advanced settings*: **Allow public client flows = Yes** (lo usa el login por código).
   Guardar.
7. Pedí la aprobación a Mojang en <https://aka.ms/mce-reviewappid> (piden el Client ID, el nombre del launcher y un
   contacto). Hasta que aprueben, Microsoft responde `Invalid app registration`. La aprobación es manual, puede
   tardar y no está garantizada.
8. Pegá el Client ID en `Velin/Services/MicrosoftAuthConfig.cs` (`ClientId`), recompilá (ver
   [COMPILAR.md](COMPILAR.md)) y publicá.

## Para probar sin recompilar
`data\settings.json` acepta `"MicrosoftClientId": "..."`. Solo sirve para pruebas: si está vacío se usa el de
`MicrosoftAuthConfig`.

## Qué no está verificado
El flujo completo contra los servidores reales de Microsoft/Xbox/Mojang **no se pudo probar** (hace falta un Client ID
aprobado). Solo hay pruebas automáticas del servidor local que recibe la redirección del navegador.
