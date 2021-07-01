﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xamarin.Essentials;

using MsalAuthenticationResult = Microsoft.Identity.Client.AuthenticationResult;
using MsalException = Microsoft.Identity.Client.MsalException;
using MsalIPublicClientApplication = Microsoft.Identity.Client.IPublicClientApplication;
using MsalUiRequiredException = Microsoft.Identity.Client.MsalUiRequiredException;

namespace UnoDrive.Authentication
{
    public interface IAuthenticationService
    {
        Task<IAuthenticationResult> AcquireSilentTokenAsync();
        Task<IAuthenticationResult> AcquireInteractiveTokenAsync();
        Task SignOutAsync();
    }

    public class AuthenticationService : IAuthenticationService
    {
        private string[] _scopes;
        protected MsalIPublicClientApplication PublicClientApp { get; }
        protected ILogger Logger { get; }
        public AuthenticationService(
            MsalIPublicClientApplication publicClientApp,
            ILogger<AuthenticationService> logger)
        {
            PublicClientApp = publicClientApp;
            Logger = logger;
            _scopes = new[] { "user.read" };
        }

        public async Task<IAuthenticationResult> AcquireInteractiveTokenAsync()
        {
            string message = string.Empty;
            MsalAuthenticationResult authResult = null;

            try
            {
                Logger.LogInformation("Attempting to perform interactive sign in");
                Logger.LogInformation($"Authentication Scopes: {JsonConvert.SerializeObject(_scopes)}");

                authResult = await PublicClientApp
                    .AcquireTokenInteractive(_scopes)
                    .ExecuteAsync();
            }
            catch (MsalException ex)
            {
                Logger.LogError(ex, ex.Message);
                message = ex.Message;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                message = "Unable to sign-in, try again";
            }

            if (authResult == null)
            {
                Logger.LogError("Unable to retrieve Access Token from Azure Active Directory; 'authResult' is null");

                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                    Logger.LogCritical("NO INTERNET CONNECTION: Internet required to retrieve an Access Token for the first time");
            }

            var result = authResult.ToAuthenticationResult(message, authResult?.Account?.HomeAccountId.ObjectId);

            // TODO - publish event
            //EventAggregator.GetEvent<AuthenticationChanged>().Publish(result);
            return result;
        }

        public async Task<IAuthenticationResult> AcquireSilentTokenAsync()
        {
            string message = string.Empty;
            MsalAuthenticationResult msalAuthResult = null;

            var accounts = await PublicClientApp.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            if (firstAccount == null)
            {
                Logger.LogInformation("Unable to find Account in MSAL.NET Cache");
                return default(AuthenticationResult);
            }

            if (accounts.Any())
                Logger.LogInformation($"Number of Accounts: {accounts.Count()}");


            try
            {
                Logger.LogInformation("Attempting to perform silent sign in . . .");
                Logger.LogInformation($"Authentication Scopes: {JsonConvert.SerializeObject(_scopes)}");
                Logger.LogInformation($"Account Name: {firstAccount.Username}");

                msalAuthResult = await PublicClientApp
                    .AcquireTokenSilent(_scopes, firstAccount)
                    .WithForceRefresh(false)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                Logger.LogWarning(ex, ex.Message);
                Logger.LogWarning("Unable to retrieve silent sign in Access Token");
                message = ex.Message;
            }
            catch (HttpRequestException ex)
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                    Logger.LogError(ex, ex.Message);
                else if (firstAccount != null)
                {
                    Logger.LogWarning($"Access Token expired, assuming user is {firstAccount.Username}");
                    var offlineResult = new OfflineAuthenticationResult
                    {
                        IsSuccess = true,
                        Message = "OFFLINE",
                        ObjectId = firstAccount?.HomeAccountId?.ObjectId
                    };

                    // TODO - publish event
                    //EventAggregator.GetEvent<AuthenticationChanged>().Publish(offlineResult);
                    return offlineResult;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, ex.Message);
                Logger.LogWarning("Unable to retrieve silent sign in Access Token");
                message = ex.Message;
            }

            if (!string.IsNullOrEmpty(msalAuthResult?.AccessToken))
                Logger.LogInformation("Successfully acquired Access Token from silent sign in");

            var result = msalAuthResult.ToAuthenticationResult(message, firstAccount?.HomeAccountId?.ObjectId);

            // TODO - publish event
            //EventAggregator.GetEvent<AuthenticationChanged>().Publish(result);
            return result;
        }

        public async Task SignOutAsync()
        {
            var accounts = await PublicClientApp.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();
            if (firstAccount != null)
            {
                await PublicClientApp.RemoveAsync(firstAccount);
                Logger.LogInformation($"Removed account: {firstAccount.Username}, user succesfully logged out");
            }
        }
    }
}
