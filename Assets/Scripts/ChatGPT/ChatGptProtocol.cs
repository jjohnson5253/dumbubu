using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Dumbubu.ChatGPT
{
    // Kept independent of Unity so the authentication boundary can be tested offline.
    public static class ChatGptProtocol
    {
        public const string Issuer = "https://auth.openai.com";
        public const string Resource = "https://api.openai.com/v1";
        public const string Scope = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";

        public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] DecodeBase64Url(string value)
        {
            value = value.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));
        }

        public static string RandomValue()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Base64Url(bytes);
        }

        public static string Challenge(string verifier)
        {
            using (var sha = SHA256.Create()) return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        }

        public static string Query(IEnumerable<KeyValuePair<string, string>> values) => string.Join("&",
            values.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

        public static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            foreach (string part in query.TrimStart('?').Split('&'))
            {
                if (part.Length == 0) continue;
                var pair = part.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
                if (result.ContainsKey(key)) throw new ChatGptException("The sign-in callback could not be verified.");
                result.Add(key, pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "");
            }
            return result;
        }

        public static string ValidateCallback(Dictionary<string, string> query, string state, string existingClient)
        {
            if (!query.TryGetValue("state", out string returnedState) || returnedState != state)
                throw new ChatGptException("The sign-in callback could not be verified.");
            if (query.ContainsKey("error")) throw new ChatGptException("Sign-in was declined. Continue with ChatGPT to try again.");
            if (!query.TryGetValue("code", out string code) || string.IsNullOrWhiteSpace(code))
                throw new ChatGptException("ChatGPT did not return a sign-in code.");
            query.TryGetValue("client_id", out string issued);
            if (existingClient != null && issued != null && issued != existingClient)
                throw new ChatGptException("ChatGPT returned a different account registration.");
            issued = issued ?? existingClient;
            if (string.IsNullOrWhiteSpace(issued) || issued == "dynamic_agent_client")
                throw new ChatGptException("ChatGPT registration was incomplete. Please try again.");
            return issued;
        }

        public static JObject ValidateIdToken(string token, JObject jwks, string clientId, string nonce, string subject = null)
        {
            try
            {
                string[] parts = token.Split('.');
                if (parts.Length != 3) throw new FormatException();
                var header = JObject.Parse(Encoding.UTF8.GetString(DecodeBase64Url(parts[0])));
                if ((string)header["alg"] != "RS256") throw new FormatException();
                var key = jwks["keys"].FirstOrDefault(k => (string)k["kid"] == (string)header["kid"] && (string)k["kty"] == "RSA");
                if (key == null) throw new FormatException();
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters { Modulus = DecodeBase64Url((string)key["n"]), Exponent = DecodeBase64Url((string)key["e"]) });
                    if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), DecodeBase64Url(parts[2]),
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new FormatException();
                }
                var claims = JObject.Parse(Encoding.UTF8.GetString(DecodeBase64Url(parts[1])));
                var audience = claims["aud"];
                bool matchesAudience = audience is JArray ? audience.Values<string>().Contains(clientId) : (string)audience == clientId;
                if (audience is JArray audiences && audiences.Count > 1 && (string)claims["azp"] != clientId) throw new FormatException();
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if ((string)claims["iss"] != Issuer || !matchesAudience || (long?)claims["exp"] <= now || claims["exp"] == null ||
                    (long?)claims["nbf"] > now + 60 || (nonce != null && (string)claims["nonce"] != nonce) ||
                    string.IsNullOrWhiteSpace((string)claims["sub"]) || (subject != null && (string)claims["sub"] != subject)) throw new FormatException();
                return claims;
            }
            catch (Exception error) when (!(error is ChatGptException))
            {
                throw new ChatGptException("ChatGPT's account identity could not be verified.");
            }
        }

        // Speech is shown only after the terminal success event, never a partial failed answer.
        public static string ReadSpeech(string stream)
        {
            var text = new StringBuilder();
            bool completed = false;
            bool failed = false;
            string failureCode = null;
            foreach (string line in stream.Replace("\r\n", "\n").Split('\n'))
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                string data = line.Substring(5).Trim();
                if (data == "[DONE]" || data.Length == 0) continue;
                var item = JObject.Parse(data);
                string type = (string)item["type"];
                if (type == "response.output_text.delta") text.Append((string)item["delta"]);
                if (type == "response.completed") completed = (string)item["response"]?["status"] == "completed";
                if (type == "error" || type == "response.failed" || type == "response.incomplete")
                {
                    // Some streams send a generic error before the detailed terminal failure.
                    failed = true;
                    failureCode = (string)item["response"]?["error"]?["code"] ??
                        (string)item["error"]?["code"] ?? (string)item["code"] ?? failureCode;
                }
            }
            if (failed) throw Error(failureCode);
            if (!completed) throw new ChatGptException("Dumbubu lost the connection. Trying again shortly.");
            string speech = string.Join(" ", text.ToString().Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim('"');
            if (speech.Length == 0) throw new ChatGptException("Dumbubu didn't find its words. Trying again shortly.");
            if (speech.Length > 180) speech = speech.Substring(0, 177).TrimEnd() + "…";
            return speech;
        }

        public static ChatGptException Error(string code, int status = 0)
        {
            if (code == "subscription_sharing_usage_limit_exceeded")
                return new ChatGptException("ChatGPT app usage limit reached. Check Usage settings, then Test speech now when access is available.", pausesSpeech: true, code: code);
            if (code == "subscription_sharing_user_not_eligible")
                return new ChatGptException("This ChatGPT account cannot share its plan with Dumbubu. Check your plan or choose another account.", pausesSpeech: true, code: code);
            if (status == 401 || code == "invalid_grant" || code == "invalid_token")
                return new ChatGptException("Please reconnect your ChatGPT account.", true);
            if (code == "subscription_sharing_usage_unavailable" || status == 429)
                return new ChatGptException("ChatGPT usage is unavailable right now. Dumbubu will try again later.");
            if (status == 403) return new ChatGptException("Enable ChatGPT plan access for Dumbubu, then reconnect.", true);
            return new ChatGptException("ChatGPT couldn't complete the request. Trying again shortly.");
        }
    }

    public sealed class ChatGptException : Exception
    {
        public bool RequiresSignIn { get; }
        public bool PausesSpeech { get; }
        public string Code { get; }
        public ChatGptException(string message, bool requiresSignIn = false, bool pausesSpeech = false, string code = null) : base(message)
        {
            RequiresSignIn = requiresSignIn;
            PausesSpeech = pausesSpeech;
            Code = code;
        }
    }
}
