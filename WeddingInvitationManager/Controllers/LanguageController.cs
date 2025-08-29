using Microsoft.AspNetCore.Mvc;
using WeddingInvitationManager.Services;

namespace WeddingInvitationManager.Controllers
{
    public class LanguageController : Controller
    {
        private readonly LanguageService _languageService;

        public LanguageController(LanguageService languageService)
        {
            _languageService = languageService;
        }

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (string.IsNullOrEmpty(culture))
            {
                culture = "en";
            }

            var supportedLanguages = LanguageService.GetSupportedLanguages();
            if (!supportedLanguages.Any(l => l.Code == culture))
            {
                culture = "en";
            }

            _languageService.SetLanguage(culture);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
