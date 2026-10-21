namespace Velin.Services
{
    // Configuración del login de Microsoft. El Client ID NO es un secreto: es el identificador PÚBLICO
    // de la app de Azure de Velin (cualquier launcher del mundo lo lleva dentro del programa).
    // Se completa UNA sola vez, por el desarrollador, después de registrar la app en Azure y de que
    // Mojang la apruebe (https://aka.ms/mce-reviewappid). Los usuarios nunca ven ni cargan nada de esto.
    public static class MicrosoftAuthConfig
    {
        public const string ClientId = ""; // <- pegá acá el "Application (client) ID" de tu app de Azure
    }
}
