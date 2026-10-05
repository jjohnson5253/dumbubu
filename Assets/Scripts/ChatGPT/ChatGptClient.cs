using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Dumbubu.ChatGPT
{
    public sealed class ChatGptClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly ChatGptCredentialStore store;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private string catalogCheckedClientId;
        public ChatGptRegistrations Registrations { get; }
        public ChatGptAccount ActiveAccount => Registrations.Accounts.FirstOrDefault(a => a.ClientId == Registrations.ActiveClientId);
        public bool Connected => ActiveAccount != null && ActiveAccount.Connected;

        public ChatGptClient(string directory, HttpMessageHandler handler = null)
        {
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(25);
            store = new ChatGptCredentialStore(directory);
            try
            {
                store.AcquireSessionLock();
                Registrations = store.Load();
                store.Save(Registrations); // Persist the host ID before the first authorization.
            }
            catch { store.Dispose(); http.Dispose(); throw; }
        }

        public async Task SignInAsync(string existingClientId, Action<string> openBrowser, CancellationToken cancellation)
        {
            await gate.WaitAsync(cancellation);
            try
            {
                var existing = Registrations.Accounts.FirstOrDefault(a => a.ClientId == existingClientId);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                using (var callback = new ChatGptLoopback())
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(3));
                    string state = ChatGptProtocol.RandomValue(), nonce = ChatGptProtocol.RandomValue(), verifier = ChatGptProtocol.RandomValue();
                    var parameters = new Dictionary<string, string> {
                        { "client_id", existing?.ClientId ?? "dynamic_agent_client" }, { "ext_agent_host_id", Registrations.HostId },
                        { "response_type", "code" }, { "redirect_uri", callback.RedirectUri }, { "scope", ChatGptProtocol.Scope },
                        { "resource", ChatGptProtocol.Resource }, { "state", state }, { "nonce", nonce },
                        { "code_challenge_method", "S256" }, { "code_challenge", ChatGptProtocol.Challenge(verifier) }
                    };
                    if (existing == null) parameters.Add("agent_name_hint", "Dumbubu");
                    else
                    {
                        if (!string.IsNullOrEmpty(existing.IdToken)) parameters.Add("id_token_hint", existing.IdToken);
                        if (!string.IsNullOrEmpty(existing.Email)) parameters.Add("login_hint", existing.Email);
                    }
                    openBrowser(ChatGptProtocol.Issuer + "/api/accounts/authorize?" + ChatGptProtocol.Query(parameters));
                    var query = await callback.WaitAsync(state, timeout.Token);
                    string clientId = ChatGptProtocol.ValidateCallback(query, state, existing?.ClientId);
                    JObject tokens = await PostFormAsync(ChatGptProtocol.Issuer + "/api/accounts/oauth/token", new Dictionary<string, string> {
                        { "grant_type", "authorization_code" }, { "client_id", clientId }, { "code", query["code"] },
                        { "code_verifier", verifier }, { "redirect_uri", callback.RedirectUri }, { "resource", ChatGptProtocol.Resource }
                    }, timeout.Token);
                    JObject jwks = await GetJsonAsync(ChatGptProtocol.Issuer + "/.well-known/jwks.json", null, timeout.Token);
                    JObject identity = ChatGptProtocol.ValidateIdToken((string)tokens["id_token"], jwks, clientId, nonce, existing?.Subject);
                    var account = new ChatGptAccount { ClientId = clientId, Subject = (string)identity["sub"], Email = (string)identity["email"] };
                    ApplyTokens(account, tokens);
                    await SelectModelAsync(account, timeout.Token);
                    // Activate only after identity, permissions, and model discovery all succeed.
                    int index = Registrations.Accounts.FindIndex(a => a.ClientId == clientId && a.Subject == account.Subject);
                    if (index < 0) Registrations.Accounts.Add(account); else Registrations.Accounts[index] = account;
                    Registrations.ActiveClientId = clientId;
                    catalogCheckedClientId = clientId;
                    store.Save(Registrations);
                }
            }
            finally { gate.Release(); }
        }

        public async Task<string> SpeakAsync(string instructions, string context, IEnumerable<string> recentLines, CancellationToken cancellation)
        {
            await gate.WaitAsync(cancellation);
            try
            {
                if (!Connected) throw new ChatGptException("Connect ChatGPT to let Dumbubu speak.", true);
                var account = ActiveAccount;
                await RefreshAsync(account, cancellation);
                if (string.IsNullOrEmpty(account.Model) || catalogCheckedClientId != account.ClientId)
                {
                    await SelectModelAsync(account, cancellation);
                    catalogCheckedClientId = account.ClientId;
                }
                var input = new JArray();
                foreach (string line in recentLines) input.Add(new JObject { ["role"] = "assistant", ["content"] = line });
                input.Add(new JObject { ["role"] = "user", ["content"] = context + "\nSay one new, short line as Dumbubu. The player cannot reply." });
                var payload = new JObject { ["model"] = account.Model, ["instructions"] = instructions, ["input"] = input, ["store"] = false, ["stream"] = true };
                using (var request = new HttpRequestMessage(HttpMethod.Post, ChatGptProtocol.Resource + "/responses"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                    request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                    // Buffer this short SSE response so an interrupted/failed line is never shown.
                    using (var response = await http.SendAsync(request, cancellation))
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) account.Model = null;
                            throw HttpError(body, (int)response.StatusCode);
                        }
                        return ChatGptProtocol.ReadSpeech(body);
                    }
                }
            }
            catch (ChatGptException error)
            {
                if (error.RequiresSignIn && ActiveAccount != null) { ActiveAccount.ClearTokens(); store.Save(Registrations); }
                throw;
            }
            finally { gate.Release(); }
        }

        public async Task<bool> DisconnectAsync(CancellationToken cancellation)
        {
            await gate.WaitAsync(cancellation);
            try
            {
                var account = ActiveAccount;
                if (account == null) return true;
                bool revoked = string.IsNullOrEmpty(account.RefreshToken);
                try
                {
                    if (!revoked)
                    {
                        var discovery = await GetJsonAsync(ChatGptProtocol.Issuer + "/.well-known/openid-configuration", null, cancellation);
                        string endpoint = (string)discovery["revocation_endpoint"];
                        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri) || uri.Scheme != "https" || uri.Host != "auth.openai.com")
                            throw new ChatGptException("Could not verify ChatGPT's disconnect endpoint.");
                        for (int attempt = 0; attempt < 2 && !revoked; attempt++)
                        {
                            try
                            {
                                using (var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                                    { "token", account.RefreshToken }, { "token_type_hint", "refresh_token" }, { "client_id", account.ClientId }
                                }))
                                using (var response = await http.PostAsync(endpoint, content, cancellation)) revoked = response.StatusCode == System.Net.HttpStatusCode.OK;
                            }
                            catch (HttpRequestException) { revoked = false; }
                            if (!revoked && attempt == 0) await Task.Delay(1000, cancellation);
                        }
                    }
                }
                catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException || error is ChatGptException) { revoked = false; }
                finally { account.ClearTokens(); store.Save(Registrations); }
                return revoked;
            }
            finally { gate.Release(); }
        }

        private async Task RefreshAsync(ChatGptAccount account, CancellationToken cancellation)
        {
            if (account.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60) return;
            if (string.IsNullOrEmpty(account.RefreshToken)) throw new ChatGptException("Please reconnect your ChatGPT account.", true);
            var tokens = await PostFormAsync(ChatGptProtocol.Issuer + "/api/accounts/oauth/token", new Dictionary<string, string> {
                { "grant_type", "refresh_token" }, { "client_id", account.ClientId }, { "refresh_token", account.RefreshToken }, { "resource", ChatGptProtocol.Resource }
            }, cancellation);
            if (tokens["id_token"] != null)
            {
                var jwks = await GetJsonAsync(ChatGptProtocol.Issuer + "/.well-known/jwks.json", null, cancellation);
                ChatGptProtocol.ValidateIdToken((string)tokens["id_token"], jwks, account.ClientId, null, account.Subject);
            }
            ApplyTokens(account, tokens);
            store.Save(Registrations);
        }

        private static void ApplyTokens(ChatGptAccount account, JObject tokens)
        {
            string scope = (string)tokens["scope"] ?? account.Scope;
            if (scope == null || !scope.Split(' ').Contains("chatgpt.tokens.use.direct"))
                throw new ChatGptException("Enable ChatGPT plan access for Dumbubu, then reconnect.", true);
            if (string.IsNullOrEmpty((string)tokens["access_token"]) ||
                !string.Equals((string)tokens["token_type"], "Bearer", StringComparison.OrdinalIgnoreCase) || (long?)tokens["expires_in"] <= 0 || tokens["expires_in"] == null)
                throw new ChatGptException("ChatGPT returned incomplete credentials. Please reconnect.", true);
            account.AccessToken = (string)tokens["access_token"];
            account.RefreshToken = (string)tokens["refresh_token"] ?? account.RefreshToken;
            account.IdToken = (string)tokens["id_token"] ?? account.IdToken;
            account.Scope = scope;
            account.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)tokens["expires_in"];
        }

        private async Task SelectModelAsync(ChatGptAccount account, CancellationToken cancellation)
        {
            var catalog = await GetJsonAsync(ChatGptProtocol.Resource + "/models", account.AccessToken, cancellation);
            var models = (catalog["models"] as JArray)?.Where(m => (string)m["visibility"] == "list" && !string.IsNullOrEmpty((string)m["slug"])).ToList();
            if (models == null || models.Count == 0) throw new ChatGptException("No ChatGPT models are available for this account.", true);
            // Prefer the available small model for brief ambient speech; preserve server order otherwise.
            var model = models.FirstOrDefault(m => ((string)m["slug"]).Contains("luna")) ?? models[0];
            account.Model = (string)model["slug"];
        }

        private async Task<JObject> GetJsonAsync(string url, string token, CancellationToken cancellation)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using (var response = await http.SendAsync(request, cancellation))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) throw HttpError(body, (int)response.StatusCode);
                    return JObject.Parse(body);
                }
            }
        }

        private async Task<JObject> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken cancellation)
        {
            using (var content = new FormUrlEncodedContent(form))
            using (var response = await http.PostAsync(url, content, cancellation))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) throw HttpError(body, (int)response.StatusCode);
                return JObject.Parse(body);
            }
        }

        private static ChatGptException HttpError(string body, int status)
        {
            string code = null;
            try { var json = JObject.Parse(body); code = json["error"] is JObject ? (string)json["error"]["code"] : (string)json["error"]; }
            catch (Exception) { /* Never surface provider bodies, authorization URLs, or tokens. */ }
            return ChatGptProtocol.Error(code, status);
        }

        public void Dispose() { http.Dispose(); store.Dispose(); }
    }
}
