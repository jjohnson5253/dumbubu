using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace Dumbubu.ChatGPT
{
    public sealed class ChatGptAccount
    {
        public string ClientId;
        public string Subject;
        public string Email;
        public string AccessToken;
        public string RefreshToken;
        public string IdToken;
        public string Scope;
        public long ExpiresAt;
        public string Model;
        [JsonIgnore] public bool Connected => !string.IsNullOrEmpty(AccessToken);
        public void ClearTokens() { AccessToken = RefreshToken = IdToken = Scope = null; ExpiresAt = 0; }
    }

    public sealed class ChatGptRegistrations
    {
        public string HostId = "urn:uuid:" + Guid.NewGuid();
        public string ActiveClientId;
        public List<ChatGptAccount> Accounts = new List<ChatGptAccount>();
    }

    public sealed class ChatGptCredentialStore : IDisposable
    {
        private readonly string path;
        private FileStream sessionLock;
        private static bool Windows => Path.DirectorySeparatorChar == '\\';
        [DllImport("libc", SetLastError = true)] private static extern int chmod(string path, uint mode);
        [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }

        public ChatGptCredentialStore(string directory) { path = Path.Combine(directory, "chatgpt-accounts.dat"); }

        public void AcquireSessionLock()
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            ProtectPermissions(directory, true);
            // Keep another game process from racing a rotating refresh token.
            sessionLock = new FileStream(Path.Combine(directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            ProtectPermissions(Path.Combine(directory, "session.lock"), false);
        }

        public void Dispose() { sessionLock?.Dispose(); }

        public ChatGptRegistrations Load()
        {
            if (!File.Exists(path)) return new ChatGptRegistrations();
            ProtectPermissions(path, false);
            byte[] bytes = File.ReadAllBytes(path);
            if (Windows) bytes = ProtectWindows(bytes, false);
            var registrations = JsonConvert.DeserializeObject<ChatGptRegistrations>(Encoding.UTF8.GetString(bytes));
            if (registrations == null || registrations.Accounts == null || string.IsNullOrEmpty(registrations.HostId))
                throw new InvalidDataException("Invalid ChatGPT account store.");
            return registrations;
        }

        public void Save(ChatGptRegistrations registrations)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            ProtectPermissions(directory, true);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(registrations));
            if (Windows) bytes = ProtectWindows(bytes, true);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Set permissions before writing any credential bytes.
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ProtectPermissions(temporary, false);
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void ProtectPermissions(string target, bool directory)
        {
            if (!Windows && chmod(target, directory ? 448u : 384u) != 0) // 0700 / 0600
                throw new IOException("Could not protect ChatGPT credentials.");
        }

        private static byte[] ProtectWindows(byte[] bytes, bool encrypt)
        {
            var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            var output = new Blob();
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                bool success = encrypt
                    ? CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!success) throw new IOException("Could not access protected ChatGPT credentials.");
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
        }
    }
}
