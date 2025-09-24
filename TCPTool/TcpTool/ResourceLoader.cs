using System.Drawing;
using System.Windows.Forms;

namespace TcpTool
{
    public static class ResourceLoader
    {
        private static Image? _exportIcon16;
        private static Image? _importIcon;

        public static Image? LoadImportJsonIcon()
        {
            if (_importIcon != null) return _importIcon;

            var searchPaths = new[]
            {
                AppContext.BaseDirectory,
                Path.Combine(AppContext.BaseDirectory, "Resources"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources")
            };

            foreach (var folder in searchPaths)
            {
                var full = Path.Combine(folder, "import.ico");
                if (!File.Exists(full)) continue;
                try
                {
                    using var ic = new Icon(full, new Size(24,24));
                    _importIcon = ic.ToBitmap();
                    return _importIcon;
                }
                catch { }
            }
            return null;
        }

        public static Image? LoadExportIcon(int size = 16)
        {
            if (size != 16) size = 16;
            if (_exportIcon16 != null) return _exportIcon16;

            var searchPaths = new[]
            {
                AppContext.BaseDirectory,
                Path.Combine(AppContext.BaseDirectory, "Resources"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources")
            };

            foreach (var folder in searchPaths)
            {
                var full = Path.Combine(folder, "export4.ico");
                if (!File.Exists(full)) continue;
                try
                {
                    using var ic = new Icon(full, new Size(16,16));
                    _exportIcon16 = ic.ToBitmap();
                    return _exportIcon16;
                }
                catch { }
            }
            return null;
        }
    }
}
