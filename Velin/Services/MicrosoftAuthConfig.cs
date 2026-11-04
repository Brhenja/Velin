namespace Velin.Services
{
    // Configuración del login de Microsoft. El Client ID NO es un secreto: es el identificador PÚBLICO
    // de la app de Azure de Velin (cualquier launcher del mundo lo lleva dentro del programa).
    // Lo completa el desarrollador una sola vez, después de registrar la app en Azure y de que
    // Mojang la apruebe (https://aka.ms/mce-reviewappid). Los usuarios nunca ven ni cargan nada de esto.
    public static class MicrosoftAuthConfig
    {
        public const string ClientId = "c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb"; // app de Azure de Velin (aprobada por Mojang)
    }
}
