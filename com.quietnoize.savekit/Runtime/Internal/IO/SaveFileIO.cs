namespace QuietNoize.SaveKit.Internal.IO
{
    using System.IO;
    using System.Threading.Tasks;

    internal static class SaveFileIO
    {
        public static void Write(string content, string path)
        {
            File.WriteAllText(path, content);
        }

        public static async Task WriteAsync(string content, string path)
        {
            await File.WriteAllTextAsync(path, content);
        }

        public static string Read(string path)
        {
            return File.ReadAllText(path);
        }

        public static async Task<string> ReadAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }
    }
}