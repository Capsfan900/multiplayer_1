using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Networking;

[assembly: InternalsVisibleTo("Wildstone.Fusion.EditModeTests")]

namespace Wildstone.Networking
{
    // Call on Unity's main thread. Credentials and authentication values must never be logged or persisted.
    public sealed class PlayFabIdentity
    {
        readonly string titleId;
        readonly string photonApplicationId;
        const int TimeoutSeconds = 20;
        const int MaximumResponseBytes = 256 * 1024;

        public sealed class Login
        {
            public string PlayFabId { get; }
            public string DisplayName { get; }
            public AuthenticationValues Authentication { get; }

            internal Login(string playerId, string displayName, string token)
            {
                PlayFabId = playerId;
                DisplayName = displayName;
                Authentication = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
                Authentication.AddAuthParameter("username", playerId);
                Authentication.AddAuthParameter("token", token);
            }
        }

        public PlayFabIdentity(string titleId, string photonApplicationId)
        {
            if (titleId == null || !Regex.IsMatch(titleId, "\\A[0-9a-fA-F]{1,16}\\z"))
                throw new ArgumentException("A valid PlayFab Title ID is required.");
            if (!Guid.TryParse(photonApplicationId, out var appId) || appId == Guid.Empty)
                throw new ArgumentException("A valid Photon application ID is required.");
            this.titleId = titleId;
            this.photonApplicationId = appId.ToString();
        }

        public async Task<Login> LoginAsync(string email, string password, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateCredentials(email, password);
            var account = await PostAsync("LoginWithEmailAddress", new LoginRequest
            {
                TitleId = titleId, Email = email, Password = password,
                InfoRequestParameters = new InfoRequest { GetUserAccountInfo = true }
            }, null, cancellation);
            RequireValue(account.PlayFabId);
            RequireValue(account.SessionTicket);
            var photon = await PostAsync("GetPhotonAuthenticationToken", new PhotonRequest
            { PhotonApplicationId = photonApplicationId }, account.SessionTicket, cancellation);
            cancellation.ThrowIfCancellationRequested();
            return CompleteLogin(account, photon);
        }

        public async Task<Login> RegisterAsync(string email, string password, string displayName,
            CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateCredentials(email, password);
            ValidateText(displayName, 25, "A display name is required.");
            if (displayName.Length < 3) throw new ArgumentException("Display name must have at least three characters.");
            await PostAsync("RegisterPlayFabUser", new RegisterRequest
            {
                TitleId = titleId, Email = email, Password = password, DisplayName = displayName,
                RequireBothUsernameAndEmail = false
            }, null, cancellation);
            // Re-read the provider's profile; registration does not return the authoritative display name.
            return await LoginAsync(email, password, cancellation);
        }

        public async Task SendRecoveryEmailAsync(string email, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateText(email, 254, "An email address is required.");
            await PostAsync("SendAccountRecoveryEmail", new RecoveryRequest { TitleId = titleId, Email = email }, null, cancellation);
        }

        async Task<ResponseData> PostAsync(string endpoint, object body, string session, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
            using (var request = new UnityWebRequest("https://" + titleId + ".playfabapi.com/Client/" + endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new BoundedDownload(MaximumResponseBytes);
                request.timeout = TimeoutSeconds;
                request.redirectLimit = 0;
                request.SetRequestHeader("Content-Type", "application/json");
                if (session != null) request.SetRequestHeader("X-Authorization", session);
                try
                {
                    var pending = request.SendWebRequest();
                    while (!pending.isDone)
                    {
                        if (cancellation.IsCancellationRequested)
                        {
                            request.Abort();
                            cancellation.ThrowIfCancellationRequested();
                        }
                        await Task.Yield();
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new InvalidOperationException("PlayFab request failed. Check your credentials, service configuration and connection, then retry.");
                    return ParseResponse(request.downloadHandler.text);
                }
                finally { Array.Clear(bytes, 0, bytes.Length); ((BoundedDownload)request.downloadHandler).Clear(); }
            }
        }

        internal static ResponseData ParseResponse(string json)
        {
            Envelope response;
            try { response = JsonUtility.FromJson<Envelope>(json); }
            catch (Exception) { throw InvalidResponse(); }
            if (response == null || response.code != 200 || response.status != "OK" || response.data == null)
                throw InvalidResponse();
            return response.data;
        }

        internal static Login CompleteLogin(ResponseData account, ResponseData photon)
        {
            RequireValue(account?.PlayFabId);
            RequireValue(photon?.PhotonCustomAuthenticationToken);
            return new Login(account.PlayFabId, account.InfoResultPayload?.AccountInfo?.TitleInfo?.DisplayName ?? "",
                photon.PhotonCustomAuthenticationToken);
        }

        static void ValidateCredentials(string email, string password)
        {
            ValidateText(email, 254, "An email address is required.");
            ValidateText(password, 100, "A password is required.");
        }

        static void ValidateText(string value, int maximum, string error)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException(error);
        }

        static void RequireValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 16384 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw InvalidResponse();
        }
        static InvalidOperationException InvalidResponse() => new InvalidOperationException("PlayFab returned an invalid response. Please retry.");

        sealed class BoundedDownload : DownloadHandlerScript
        {
            readonly byte[] content;
            int count;
            public BoundedDownload(int maximum) : base(new byte[8192]) { content = new byte[maximum]; }
            protected override bool ReceiveData(byte[] data, int length)
            {
                if (data == null || length < 0 || length > content.Length - count) return false;
                Buffer.BlockCopy(data, 0, content, count, length);
                count += length;
                return true;
            }
            protected override string GetText() => Encoding.UTF8.GetString(content, 0, count);
            protected override byte[] GetData()
            {
                var result = new byte[count];
                Buffer.BlockCopy(content, 0, result, 0, count);
                return result;
            }
            public void Clear()
            {
                Array.Clear(content, 0, content.Length);

            }
        }

        [Serializable] sealed class LoginRequest { public string TitleId, Email, Password; public InfoRequest InfoRequestParameters; }
        [Serializable] sealed class InfoRequest { public bool GetUserAccountInfo; }
        [Serializable] sealed class RegisterRequest { public string TitleId, Email, Password, DisplayName; public bool RequireBothUsernameAndEmail; }
        [Serializable] sealed class RecoveryRequest { public string TitleId, Email; }
        [Serializable] sealed class PhotonRequest { public string PhotonApplicationId; }
        [Serializable] sealed class Envelope { public int code; public string status; public ResponseData data; }
        [Serializable] internal sealed class ResponseData
        {
            public string PlayFabId, SessionTicket, PhotonCustomAuthenticationToken;
            public CombinedInfo InfoResultPayload;
        }
        [Serializable] internal sealed class CombinedInfo { public AccountInfo AccountInfo; }
        [Serializable] internal sealed class AccountInfo { public TitleInfo TitleInfo; }
        [Serializable] internal sealed class TitleInfo { public string DisplayName; }
    }
}

