using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dumbubu.ChatGPT;
using Newtonsoft.Json.Linq;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ChatGptException) { checks++; return; }
        throw new Exception(message);
    }

    private static async Task Main()
    {
        Check(ChatGptProtocol.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "PKCE RFC 7636 vector failed.");
        var callback = ChatGptProtocol.ParseQuery("?state=expected&code=abc&client_id=oaiapp_test");
        Check(ChatGptProtocol.ValidateCallback(callback, "expected", null) == "oaiapp_test", "Issued ID missing.");
        Reject(() => ChatGptProtocol.ValidateCallback(callback, "wrong", null), "Wrong state accepted.");
        Reject(() => ChatGptProtocol.ValidateCallback(callback, "expected", "oaiapp_other"), "Changed client accepted.");
        Reject(() => ChatGptProtocol.ValidateCallback(ChatGptProtocol.ParseQuery("state=expected&code=abc&client_id=dynamic_agent_client"), "expected", null), "Entrypoint saved as issued ID.");
        Reject(() => ChatGptProtocol.ParseQuery("state=one&state=two"), "Duplicate state accepted.");
        Reject(() => ChatGptProtocol.ValidateCallback(ChatGptProtocol.ParseQuery("state=expected&error=access_denied"), "expected", null), "Denied auth accepted.");

        using (var provider = new FakeOpenAI())
        {
            string jwt = provider.Token("oaiapp_test", "nonce", "subject");
            Check((string)ChatGptProtocol.ValidateIdToken(jwt, provider.Jwks, "oaiapp_test", "nonce")["sub"] == "subject", "Signed JWT rejected.");
            Reject(() => ChatGptProtocol.ValidateIdToken(jwt, provider.Jwks, "oaiapp_other", "nonce"), "Wrong audience accepted.");
            Reject(() => ChatGptProtocol.ValidateIdToken(jwt, provider.Jwks, "oaiapp_test", "wrong"), "Wrong nonce accepted.");
            Reject(() => ChatGptProtocol.ValidateIdToken(jwt, provider.Jwks, "oaiapp_test", "nonce", "different"), "Wrong identity accepted.");
            Reject(() => ChatGptProtocol.ValidateIdToken(provider.Token("oaiapp_test", "nonce", "subject", -1), provider.Jwks, "oaiapp_test", "nonce"), "Expired identity accepted.");
            Reject(() => ChatGptProtocol.ValidateIdToken(provider.Token("oaiapp_test", "nonce", "subject", 300, "https://other.example"), provider.Jwks, "oaiapp_test", "nonce"), "Wrong issuer accepted.");
            string[] parts = jwt.Split('.');
            byte[] signature = ChatGptProtocol.DecodeBase64Url(parts[2]); signature[0] ^= 1;
            Reject(() => ChatGptProtocol.ValidateIdToken(parts[0] + "." + parts[1] + "." + ChatGptProtocol.Base64Url(signature), provider.Jwks, "oaiapp_test", "nonce"), "Forged signature accepted.");
        }
        Check(ChatGptProtocol.ReadSpeech(FakeOpenAI.Speech) == "I am extremely round today.", "Completed speech missing.");
        Reject(() => ChatGptProtocol.ReadSpeech("data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n"), "Interrupted speech accepted.");
        Reject(() => ChatGptProtocol.ReadSpeech("data: {\"type\":\"response.failed\",\"response\":{\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\"}}}\n"), "Failed speech accepted.");
        Reject(() => ChatGptProtocol.ReadSpeech("data: {\"type\":\"response.completed\",\"response\":{\"status\":\"failed\"}}\n"), "Failed terminal status accepted.");
        try { ChatGptProtocol.ReadSpeech(FakeOpenAI.QuotaFailure); throw new Exception("Quota failure accepted."); }
        catch (ChatGptException error)
        {
            Check(error.PausesSpeech && !error.RequiresSignIn, "Usage limit did not pause speech while preserving sign-in.");
            Check(error.Code == "subscription_sharing_usage_limit_exceeded", "Generic stream error masked the terminal usage error.");
        }
        Reject(() => ChatGptProtocol.ReadSpeech("data: {\"type\":\"error\",\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\"}}\n" + FakeOpenAI.Speech), "Completion hid an earlier error.");
        Check(ChatGptProtocol.Error("subscription_sharing_user_not_eligible", 403).PausesSpeech, "Ineligible account loops through sign-in.");

        await TestLoopback();
        await TestClient();
        Console.WriteLine("Passed " + checks + " ChatGPT checks (offline; no real account or model calls).");
    }

    private static async Task TestLoopback()
    {
        using (var callback = new ChatGptLoopback())
        using (var browser = new HttpClient(new HttpClientHandler { UseProxy = false }))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            Task<Dictionary<string, string>> pending = callback.WaitAsync("state", timeout.Token);
            using (var bad = await browser.GetAsync(callback.RedirectUri + "?state=wrong&code=stolen")) Check(bad.StatusCode == HttpStatusCode.BadRequest, "Wrong-state request was accepted by loopback.");
            using (var good = await browser.GetAsync(callback.RedirectUri + "?state=state&code=right&client_id=oaiapp_test")) Check(good.StatusCode == HttpStatusCode.OK, "Valid callback failed.");
            Check((await pending)["code"] == "right", "Invalid callback poisoned authorization.");
        }
        using (var callback = new ChatGptLoopback())
        using (var cancellation = new CancellationTokenSource())
        {
            Task pending = callback.WaitAsync("state", cancellation.Token);
            cancellation.Cancel();
            try { await pending; throw new Exception("Callback didn't cancel."); }
            catch (OperationCanceledException) { checks++; }
        }
    }

    private static async Task TestClient()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dumbubu-chatgpt-test-" + Guid.NewGuid());
        try
        {
            var provider = new FakeOpenAI();
            string hostId;
            using (var client = new ChatGptClient(directory, provider))
            {
                hostId = client.Registrations.HostId;
                try { using (var duplicate = new ChatGptClient(directory, new FakeOpenAI())) { } throw new Exception("Two game processes can race the same token store."); }
                catch (IOException) { checks++; }
                await client.SignInAsync(null, provider.Browser, CancellationToken.None);
                Check(client.Connected && client.ActiveAccount.ClientId == "oaiapp_test", "Sign-in did not activate account.");
                Check(provider.Auth["client_id"] == "dynamic_agent_client" && provider.Auth["agent_name_hint"] == "Dumbubu", "Initial registration parameters incorrect.");
                Check(provider.Grant["client_id"] == "oaiapp_test" && provider.Grant["resource"] == ChatGptProtocol.Resource && !provider.Grant.ContainsKey("client_secret"), "Incorrect public token exchange.");
                Check(ChatGptProtocol.Challenge(provider.Grant["code_verifier"]) == provider.Auth["code_challenge"], "PKCE verifier changed during exchange.");
                Check(provider.Grant["redirect_uri"] == provider.Auth["redirect_uri"], "Redirect URI changed during exchange.");
                string line = await client.SpeakAsync("skill text", "game context", new[] { "earlier thought" }, CancellationToken.None);
                Check(line == "I am extremely round today.", "End-to-end speech failed.");
                Check((bool)provider.Payload["stream"] && !(bool)provider.Payload["store"], "Required plan flags missing.");
                Check((string)provider.Payload["instructions"] == "skill text" && (string)provider.Payload["input"][0]["content"] == "earlier thought", "Skill or recent history omitted.");
                Check(provider.Payload["tools"] == null && provider.Payload["max_output_tokens"] == null, "Unsupported request fields or tools present.");
                provider.QuotaExceeded = true;
                try { await client.SpeakAsync("skill", "context", Array.Empty<string>(), CancellationToken.None); throw new Exception("Usage limit accepted."); }
                catch (ChatGptException error) { Check(error.PausesSpeech, "Client lost quota pause instruction."); }
                Check(client.Connected && new ChatGptCredentialStore(directory).Load().Accounts[0].AccessToken != null, "Quota failure erased a valid sign-in.");
                provider.QuotaExceeded = false;
                provider.DenyScope = true;
                try { await client.SignInAsync(null, provider.Browser, CancellationToken.None); throw new Exception("Missing plan scope accepted."); }
                catch (ChatGptException) { checks++; }
                Check(client.Connected && client.ActiveAccount.AccessToken == "access-original", "Failed sign-in replaced active account.");
                provider.DenyScope = false;
                await client.SignInAsync(client.ActiveAccount.ClientId, provider.Browser, CancellationToken.None);
                Check(provider.Auth["client_id"] == "oaiapp_test" && !provider.Auth.ContainsKey("agent_name_hint"), "Returning registration duplicated client.");
                Check(client.Registrations.Accounts.Count == 1, "Account registration duplicated.");
                // Force expiry to exercise renewal and persistence, not just the token parser.
                client.ActiveAccount.ExpiresAt = 1;
                await client.SpeakAsync("skill", "context", Array.Empty<string>(), CancellationToken.None);
                Check(provider.Grant["grant_type"] == "refresh_token" && !provider.Grant.ContainsKey("scope"), "Refresh request wrong.");
                Check(client.ActiveAccount.RefreshToken == "refresh-rotated", "Rotating refresh token not replaced.");
                var saved = new ChatGptCredentialStore(directory).Load();
                Check(saved.Accounts[0].RefreshToken == "refresh-rotated" && saved.HostId == hostId, "Renewal wasn't persisted atomically.");
                provider.Unauthorized = true;
                try { await client.SpeakAsync("skill", "context", Array.Empty<string>(), CancellationToken.None); throw new Exception("Unauthorized inference accepted."); }
                catch (ChatGptException error) { Check(error.RequiresSignIn, "Expired access didn't request sign-in."); }
                Check(!client.Connected && new ChatGptCredentialStore(directory).Load().Accounts[0].AccessToken == null, "Rejected credentials weren't cleared.");
                provider.Unauthorized = false;
                await client.SignInAsync(client.ActiveAccount.ClientId, provider.Browser, CancellationToken.None);
                client.ActiveAccount.ExpiresAt = 1;
                await client.SpeakAsync("skill", "context", Array.Empty<string>(), CancellationToken.None);
                Check(await client.DisconnectAsync(CancellationToken.None), "Revocation not confirmed.");
                Check(!client.Connected && client.Registrations.Accounts[0].ClientId == "oaiapp_test", "Sign-out lost registration or retained access.");
                Check(provider.Revoked == "refresh-rotated", "Wrong session revoked.");
                await client.SignInAsync(client.ActiveAccount.ClientId, provider.Browser, CancellationToken.None);
                provider.RevocationFails = true;
                Check(!await client.DisconnectAsync(CancellationToken.None), "Remote failure reported revocation success.");
                Check(!client.Connected, "Failed remote revocation retained local access.");
            }
            using (var restarted = new ChatGptClient(directory, new FakeOpenAI()))
            {
                Check(!restarted.Connected && restarted.Registrations.HostId == hostId, "Sign-out or host identity didn't survive restart.");
                Check(restarted.Registrations.Accounts[0].AccessToken == null && restarted.Registrations.Accounts[0].RefreshToken == null, "Tokens persisted after sign-out.");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FakeOpenAI : HttpMessageHandler
    {
        private readonly RSA rsa = RSA.Create(2048);
        public JObject Jwks { get; }
        public Dictionary<string, string> Auth, Grant;
        public JObject Payload;
        public bool DenyScope;
        public bool Unauthorized, RevocationFails, QuotaExceeded;
        public string Revoked;
        public const string Speech = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"I am extremely round today.\"}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\"}}\n\n";
        public const string QuotaFailure = "data: {\"type\":\"error\",\"message\":\"Request failed\"}\n\ndata: {\"type\":\"response.failed\",\"response\":{\"status\":\"failed\",\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\"}}}\n\n";

        public FakeOpenAI()
        {
            var key = rsa.ExportParameters(false);
            Jwks = new JObject { ["keys"] = new JArray(new JObject { ["kid"] = "test-key", ["kty"] = "RSA", ["n"] = ChatGptProtocol.Base64Url(key.Modulus), ["e"] = ChatGptProtocol.Base64Url(key.Exponent) }) };
        }

        public string Token(string audience, string nonce, string subject, int expires = 300, string issuer = ChatGptProtocol.Issuer)
        {
            string header = ChatGptProtocol.Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"test-key\"}"));
            string payload = ChatGptProtocol.Base64Url(Encoding.UTF8.GetBytes(new JObject { ["iss"] = issuer, ["aud"] = audience, ["sub"] = subject, ["nonce"] = nonce,
                ["exp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expires, ["email"] = "player@example.test" }.ToString()));
            return header + "." + payload + "." + ChatGptProtocol.Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        public void Browser(string url)
        {
            Auth = ChatGptProtocol.ParseQuery(new Uri(url).Query);
            _ = Task.Run(async () => {
                using (var browser = new HttpClient(new HttpClientHandler { UseProxy = false }))
                using (var response = await browser.GetAsync(Auth["redirect_uri"] + "?" + ChatGptProtocol.Query(new Dictionary<string, string> {
                    { "code", "test-code" }, { "state", Auth["state"] }, { "client_id", "oaiapp_test" }
                }))) response.EnsureSuccessStatusCode();
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            string path = request.RequestUri.AbsolutePath;
            object body;
            if (path.EndsWith("jwks.json")) body = Jwks;
            else if (path.EndsWith("openid-configuration")) body = new JObject { ["revocation_endpoint"] = "https://auth.openai.com/revoke" };
            else if (path.EndsWith("/token"))
            {
                Grant = ChatGptProtocol.ParseQuery(await request.Content.ReadAsStringAsync());
                bool refresh = Grant["grant_type"] == "refresh_token";
                body = new JObject { ["access_token"] = refresh ? "access-renewed" : "access-original", ["refresh_token"] = refresh ? "refresh-rotated" : "refresh-original",
                    ["token_type"] = "Bearer", ["expires_in"] = 3600, ["scope"] = DenyScope ? "openid email" : ChatGptProtocol.Scope };
                if (!refresh) ((JObject)body)["id_token"] = Token("oaiapp_test", Auth["nonce"], "subject");
            }
            else if (path.EndsWith("/models")) body = new JObject { ["models"] = new JArray(new JObject { ["visibility"] = "list", ["slug"] = "test-luna" }) };
            else if (path.EndsWith("/responses"))
            {
                Payload = JObject.Parse(await request.Content.ReadAsStringAsync());
                Check(request.Headers.Authorization.Scheme == "Bearer", "Missing bearer authentication.");
                if (Unauthorized) return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\":{\"code\":\"invalid_token\"}}") };
                body = QuotaExceeded ? QuotaFailure : Speech;
            }
            else if (path.EndsWith("/revoke"))
            {
                Revoked = ChatGptProtocol.ParseQuery(await request.Content.ReadAsStringAsync())["token"];
                if (RevocationFails) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("") };
                body = "";
            }
            else throw new Exception("Unexpected external endpoint: " + path);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToString()) };
        }

        protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
    }
}
