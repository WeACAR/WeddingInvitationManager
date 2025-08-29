using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace WeddingInvitationManager.Services
{
    public class LanguageService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CookieName = "WeddingApp.Culture";

        public LanguageService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetLanguage(string culture)
        {
            // Map culture codes to full culture names
            var cultureName = culture switch
            {
                "ar" => "ar-SA",
                "en" => "en-US",
                _ => "en-US"
            };

            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false, // Allow JavaScript access for client-side language detection
                SameSite = SameSiteMode.Lax
            };

            // Set the culture cookie in the format expected by RequestLocalizationOptions
            _httpContextAccessor.HttpContext?.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName, 
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureName)), 
                cookieOptions);
        }

        public string GetCurrentLanguage()
        {
            // Try to get the current UI culture from the thread first
            var currentUICulture = CultureInfo.CurrentUICulture.Name;
            if (currentUICulture.StartsWith("ar"))
                return "ar";
            if (currentUICulture.StartsWith("en"))
                return "en";

            // Fallback to cookie method
            var cultureCookie = _httpContextAccessor.HttpContext?.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
            
            if (!string.IsNullOrEmpty(cultureCookie))
            {
                var cultureInfo = CookieRequestCultureProvider.ParseCookieValue(cultureCookie);
                var culture = cultureInfo?.Cultures.FirstOrDefault().Value ?? "en-US";
                
                // Return simplified culture code
                return culture.StartsWith("ar") ? "ar" : "en";
            }
            
            return "en";
        }

        public static List<(string Code, string Name, string NativeName, string Flag)> GetSupportedLanguages()
        {
            return new List<(string, string, string, string)>
            {
                ("en", "English", "English", "🇺🇸"),
                ("ar", "Arabic", "العربية", "🇸🇦")
            };
        }
    }


}
