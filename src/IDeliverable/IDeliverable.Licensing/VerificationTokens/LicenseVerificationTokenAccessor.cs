using System;

namespace IDeliverable.Licensing.VerificationTokens
{
    public class LicenseVerificationTokenAccessor
    {
        private static readonly TimeSpan _tokenRenewalInterval = TimeSpan.FromDays(14);

        public LicenseVerificationTokenAccessor(ILicenseVerificationTokenStore store)
        {
            _store = store;
            _licensingServiceClient = new LicensingServiceClient();
        }

        private readonly ILicenseVerificationTokenStore _store;
        private readonly LicensingServiceClient _licensingServiceClient;

        public LicenseVerificationToken GetLicenseVerificationToken(string productId, string licenseKey, string hostname, bool forceRenew = false)
        {
            var token = _store.Load(productId);

            // Delete the existing verification token from store if:
            // * It was issued for a different license key OR
            // * We are instructed by caller to force renewal
            if (token != null && (token.Info.LicenseKey != licenseKey || forceRenew))
            {
                _store.Clear(productId);
                token = null;
            }

            // Renew verification token from licensing server if:
            // * We don't have a token in store OR
            // * The one we have has passed the token renewal interval
            if (token == null || token.Age > _tokenRenewalInterval)
            {
                try
                {
                    token = _licensingServiceClient.VerifyLicense(productId, licenseKey, hostname);
                }
                catch (Exception ex)
                {
                    // If we have an existing token from before, return it rather than throwing.
                    if (token != null)
                        return token;

                    if (ex is LicenseVerificationTokenException)
                        throw;

                    throw new LicenseVerificationTokenException(LicenseVerificationTokenError.UnexpectedError, ex);
                }

                _store.Save(productId, token);
            }

            return token;
        }
    }
}