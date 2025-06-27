using BepInEx.Logging;
using SPT.Common.Http;
using SPT.Common.Utils;
using System;
using System.Text;
using System.Threading.Tasks;

namespace LootValuePlus
{
    public static class CustomRequestHandler
    {
        private static ManualLogSource _logger;

        public static readonly Client HttpClient;

        public static readonly string Host;

        public static readonly string SessionId;

        public static readonly bool IsLocal;

        static CustomRequestHandler()
        {
            _logger = Logger.CreateLogSource("RequestHandler");
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            foreach (string text in commandLineArgs)
            {
                if (text.Contains("BackendUrl"))
                    Host = Json.Deserialize<ServerConfig>(text.Replace("-config=", string.Empty)).BackendUrl;
                if (text.Contains("-token="))
                    SessionId = text.Replace("-token=", string.Empty);
            }

            IsLocal = Host.Contains("127.0.0.1") || Host.Contains("localhost");
            HttpClient = new Client(Host, SessionId);
        }


        private static void ValidateJson(string path, string json)
        {

        }


        public static async Task<string> PostJsonAsync(string path, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            byte[] bytes2 = await HttpClient.PostAsync(path, bytes);

            if (null == bytes2)
            {
                return null;
            }

            string @string = Encoding.UTF8.GetString(bytes2);
            ValidateJson(path, @string);
            return @string;
        }

    }
}