using System.Configuration;
using System.Web.Mvc;

namespace SimpleAuthentication.Controllers
{
    public class GeneralFunctionController : Controller
    {
        public string GetAppSettingsValue(string key)
        {
            switch (key)
            {
                case "smtpUserName": return ConfigurationManager.AppSettings["smtpUserName"].ToString();
                case "smtpPassword": return ConfigurationManager.AppSettings["smtpPassword"].ToString();
                case "smtpHost": return ConfigurationManager.AppSettings["smtpHost"].ToString();
                case "smtpPort": return ConfigurationManager.AppSettings["smtpPort"].ToString();
                case "websiteName": return ConfigurationManager.AppSettings["websiteName"].ToString();     
                default: break;
            }
            return "";
        }
    }
}