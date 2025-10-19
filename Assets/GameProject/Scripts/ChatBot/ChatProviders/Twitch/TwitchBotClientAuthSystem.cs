using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ChatBot.Runtime;
using Drboum.Utilities;
using TwitchLib.Api;
using TwitchLib.Api.Services;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using Unity.Entities;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChatBot.ChatProviders.Twitch
{
    [DisableAutoCreation]
    public partial class TwitchBotClientAuthSystem : SystemBase
    {
        private static ProcessStartInfo _processStartInfo;

        private TwitchBotAuthDevData _twitchBotAuthDevCacheData;
        protected TwitchClient TwitchBotClient;
        protected TwitchAPI TwitchAPI;
        protected LiveStreamMonitorService ChannelMonitorService;
        private string _accessToken;
        private CancellationTokenSource _cancellationTokenSource;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<TwitchBotAuthDevData>();
            RequireForUpdate<TwitchChannelSettingsPersistentData>();
            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationMonoFix;
            _processStartInfo ??= CreateDefaultBrowserProcessStartInfo();
            TwitchBotClient = CreateTwitchClient();
            TwitchAPI = new();
            ChannelMonitorService = new(TwitchAPI);
        }


        protected override void OnStartRunning()
        {
            _twitchBotAuthDevCacheData = SystemAPI.ManagedAPI.GetSingleton<TwitchBotAuthDevData>();
            var chatSettingsData = SystemAPI.ManagedAPI.GetSingleton<TwitchChannelSettingsPersistentData>();
            SubscribeTwitchBotEvents(chatSettingsData);

            _cancellationTokenSource = new CancellationTokenSource();
            _ = StartAuthProcess(ConfigInfo.AppClientId, _twitchBotAuthDevCacheData.AuthProviderUrl, _twitchBotAuthDevCacheData.WaitForAuthUrl, _twitchBotAuthDevCacheData.TwitchScopeString, chatSettingsData.Channel, chatSettingsData.BotUserName, _cancellationTokenSource.Token);
        }

        private void CancelAuthProcess()
        {
            _cancellationTokenSource?.Cancel();
        }

        protected async Task StartAuthProcess(string appClientId, string authProviderUrl, string waitForAuthUrl, string twitchScopes, string channelToConnectTo, string userName, CancellationToken cancellationToken)
        {
            try
            {
                var authQueryID = Guid.NewGuid().ToString("N");
                string stateQueryPart = $"state={authQueryID}";
                StartDefaultBrowserWithUrl($"https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={appClientId}&redirect_uri={authProviderUrl}&scope={twitchScopes}&{stateQueryPart}", _processStartInfo);

                var authServiceResponse = await AppHttpClientProvider.Client.GetAsync($"{waitForAuthUrl}?{stateQueryPart}", cancellationToken);
                if ( authServiceResponse.IsSuccessStatusCode )
                {
                    string accessToken = (await authServiceResponse.Content.ReadAsStringAsync()).Replace("\"", "");
                    if ( !cancellationToken.IsCancellationRequested )
                    {
                        _accessToken = accessToken;
                        ConnectTwitchBot(accessToken, channelToConnectTo, userName);
                        ConnectTwitchAPI(appClientId, accessToken, channelToConnectTo);
                    }
                }
                else
                {
                    if ( !cancellationToken.IsCancellationRequested )
                    {
                        Debug.LogError($"[Fatal] failed request for automatic token resolving at {waitForAuthUrl}, the connection with twitch can not established. detail : {authServiceResponse.StatusCode} {authServiceResponse.ReasonPhrase} {authServiceResponse.Content}");
                    }
                    Application.Quit((int)authServiceResponse.StatusCode);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Exception Caught!");
                Debug.LogException(e);
            }
        }

        private void ConnectTwitchAPI(string appClientId, string accessToken, string channelToConnectTo)
        {
            TwitchAPI.Settings.AccessToken = accessToken;
            TwitchAPI.Settings.ClientId = appClientId;
            ChannelMonitorService.SetChannelsById(new List<string> { channelToConnectTo });
            ChannelMonitorService.Start();
        }

        protected static void StartDefaultBrowserWithUrl(string url, ProcessStartInfo processStartInfo = null)
        {
            processStartInfo ??= CreateDefaultBrowserProcessStartInfo();
            processStartInfo.FileName = url;
            Process.Start(processStartInfo);
        }

        protected static ProcessStartInfo CreateDefaultBrowserProcessStartInfo()
        {
            return new ProcessStartInfo {
                UseShellExecute = true // This is necessary to open the URL in the default browser
            };
        }

        private void ConnectTwitchBot(string accessToken, string channelToConnectTo, string twitchUsername)
        {
            if ( !TwitchBotClient.IsInitialized )
            {
                var credentials = new ConnectionCredentials(twitchUsername, accessToken);
                // Initialize the client with the credentials instance, and setting a default channel to connect to.
                TwitchBotClient.Initialize(credentials, channelToConnectTo);
            }
            // Connect
            TwitchBotClient.ConnectAsync().LogException();
        }

        private TwitchLib.Client.TwitchClient CreateTwitchClient()
        {
            // Create new instance of Chat Client
            return new();
        }

        protected virtual void SubscribeTwitchBotEvents(TwitchChannelSettingsPersistentData twitchTwitchChannelCachePersistentData)
        { }

        protected override void OnUpdate()
        { }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            if ( TwitchBotClient is not null && TwitchBotClient.IsConnected )
            {
                UnsubscribeTwitchBotEvents();
                TwitchBotClient.DisconnectAsync();
            }
            CancelAuthProcess();
        }

        protected virtual void UnsubscribeTwitchBotEvents()
        { }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if ( _twitchBotAuthDevCacheData is { RevokeTokenOnLeave: true } && !string.IsNullOrEmpty(_accessToken) )
            {
                var request = AppHttpClientProvider.Client.GetAsync($"https://twitchtokengenerator.com/api/revoke/{_accessToken}");
            }

        }

        public static bool CertificateValidationMonoFix(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;

            if ( sslPolicyErrors == SslPolicyErrors.None )
            {
                return true;
            }

            foreach ( X509ChainStatus status in chain.ChainStatus )
            {
                if ( status.Status == X509ChainStatusFlags.RevocationStatusUnknown )
                {
                    continue;
                }

                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;

                bool chainIsValid = chain.Build((X509Certificate2)certificate);

                if ( !chainIsValid )
                {
                    isOk = false;
                }
            }

            return isOk;
        }
    }

    public struct TwitchChatSystemRuntimeData : IComponentData
    {
        public ChatUser BroadCasterUser;
        public char CommandIdentifier;
    }
}