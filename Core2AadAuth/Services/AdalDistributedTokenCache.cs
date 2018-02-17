using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Core2AadAuth.Services
{
    /// <summary>
    /// Caches access and refresh tokens for Azure AD
    /// </summary>
    public class AdalDistributedTokenCache : TokenCache
    {
        private readonly IDistributedCache _cache;
        private readonly IDataProtector _dataProtector;
        private readonly string _userId;

        /// <summary>
        /// Constructs a token cache
        /// </summary>
        /// <param name="cache">Distributed cache to use to store data</param>
        /// <param name="dataProtector">The protector for encrypting/decrypting the cached data</param>
        /// <param name="userId">The user's unique identifier</param>
        public AdalDistributedTokenCache(IDistributedCache cache, IDataProtector dataProtector, string userId)
        {
            _cache = cache;
            _dataProtector = dataProtector;
            _userId = userId;
            BeforeAccess = BeforeAccessNotification;
            AfterAccess = AfterAccessNotification;
        }

        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            //Called before ADAL tries to access the cache,
            //so this is where we should read from the distibruted cache
            //It sucks that ADAL's API is synchronous, so we must do a blocking call here
            byte[] cachedData = _cache.Get(GetCacheKey());
            if (cachedData != null)
            {
                //Decrypt and deserialize the cached data
                Deserialize(_dataProtector.Unprotect(cachedData));
            }
            else
            {
                //Ensures the cache is cleared in TokenCache
                Deserialize(null);
            }
        }

        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            //Called after ADAL is done accessing the token cache
            if (HasStateChanged)
            {
                //In this case the cache state has changed, maybe a new token was written
                //So we encrypt and write the data to the distributed cache
                _cache.Set(GetCacheKey(), _dataProtector.Protect(Serialize()), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });
                HasStateChanged = false;
            }
        }

        private string GetCacheKey()
        {
            return $"{_userId}_TokenCache";
        }
    }
}