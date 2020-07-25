using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace OctoMerge
{
    static class Vault
    {
        public static string GetVaultValue(string file, string key, string value, bool dumpResponseOnErrors)
        {

            string[] valueParts = value.Split(':');
            if (valueParts.Length != 3)
            {
                Console.WriteLine($"Error: {key} in {file}. Value {value} is expected to have the following fomat: 'vault:path:key', for example: 'vault:secret/mysecret:mykey'");
                Environment.Exit(1);
            }
            (string path, string vaultKey) = (valueParts[1], valueParts[2]);

            string vaultAddr = Environment.GetEnvironmentVariable("VAULT_ADDR");

            if (string.IsNullOrWhiteSpace(vaultAddr))
            {
                Console.WriteLine($"Error: VAULT_ADDR environment variable is not set. Vault value {key} in {file}");
                Environment.Exit(1);
            }

            string token = GetVaultToken();

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage();
            request.Headers.Add("X-Vault-Token", token);
            request.RequestUri = new Uri($"{vaultAddr}/v1/{path}");
            var result = client.SendAsync(request).GetAwaiter().GetResult();
            var response = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!result.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: querring vault at {vaultAddr}/v1/{path}. {(int)result.StatusCode} {result.ReasonPhrase}");
                if (dumpResponseOnErrors) Console.WriteLine($"{response}");
                Environment.Exit(1);
            }

            JObject j = (JObject)JObject.Parse(response)["data"];
            if (vaultKey == "*")
            {
                return j.ToString();
            }

            if (!j.ContainsKey(vaultKey))
            {
                Console.WriteLine($"Error: obejct at '{path}' does not contain requested key '{vaultKey}'");
                if (dumpResponseOnErrors) Console.WriteLine($"{response}");
                Environment.Exit(1);
            }

            return j[vaultKey].ToString();
        }

        private static string GetVaultToken()
        {
            string vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");

            if (!string.IsNullOrWhiteSpace(vaultToken))
            {
                return vaultToken;
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string tokenFile = Path.Combine(home, ".vault-token");

            if (!File.Exists(tokenFile))
            {
                Console.WriteLine($"Error: {tokenFile} is not found.");
                Console.WriteLine($"Please login to vault first with 'vault login'");
                Environment.Exit(1);
            }

            string token = File.ReadAllText(tokenFile);
            return token;
        }

    }
}
