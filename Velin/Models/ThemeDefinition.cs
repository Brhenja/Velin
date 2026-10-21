namespace Velin.Models
{
    // Un tema completo del launcher. Los temas incorporados (Velin, Flash) viven como
    // archivos .json en Assets/Themes — y por eso cualquiera puede crear el suyo con el
    // mismo formato y agregarlo desde Configuración.
    public class ThemeDefinition
    {
        public string Name { get; set; } = "Velin";
        public string BgDark { get; set; } = "#3B2714";
        public string BgLight { get; set; } = "#6B4423";
        public string ControlBg { get; set; } = "#5A3D22";
        public string ControlText { get; set; } = "#F0E1C0";
        public string InputBg { get; set; } = "#E8D9B5";
        public string InputText { get; set; } = "#3D2914";
        public string Accent { get; set; } = "#B98A4F";
        public string ButtonTextOnAccent { get; set; } = "#2A1A0D";
        public string TitleBarBg { get; set; } = "#2A1A0D";
        public string TitleBarText { get; set; } = "#F0E1C0";
        public string ProgressBarColor { get; set; } = "#C8A165";
        public string ButtonGradientTop { get; set; } = "#7D5128";
        public string ButtonGradientBottom { get; set; } = "#5A3D22";
        public bool UseMedievalFont { get; set; } = true;
    }
}
