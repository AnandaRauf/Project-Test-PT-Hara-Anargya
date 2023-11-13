using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using SimpleAuthentication.Models;

namespace SimpleAuthentication.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private DefaultContext db = new DefaultContext();
        private GeneralFunctionController generalFunction = new GeneralFunctionController();


        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager )
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set 
            { 
                _signInManager = value; 
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            string userid = "";
            // if any of the user's input invalid, return the model to display error message
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // if username input contains @ sign, means that user use email to login
            if (model.UserName.Contains("@"))
            {
                // select the UserName of the user from AspNetUsers table and assign to model.UserName because instead of email, SignInManager use username to sign in 
                model.UserName = db.AspNetUsers.Where(a => a.Email == model.UserName).Select(a => a.UserName).DefaultIfEmpty("").FirstOrDefault();
                userid = db.AspNetUsers.Where(a => a.Email == model.UserName).Select(a => a.Id).DefaultIfEmpty("").FirstOrDefault();
            }
            userid = db.AspNetUsers.Where(a => a.UserName == model.UserName).Select(a => a.Id).DefaultIfEmpty("").FirstOrDefault();

            bool emailConfirm = db.AspNetUsers.Where(a => a.Id == userid).Select(a => a.EmailConfirmed).FirstOrDefault();
            if (emailConfirm == false)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }     
            var result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    //return RedirectToLocal(returnUrl);
                    return RedirectToAction("Index", "Home", null);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (model != null)
            {
                bool usernameExist = db.AspNetUsers.Where(a => a.UserName == model.Username).Any();
                bool emailExist = db.AspNetUsers.Where(a => a.Email == model.Email).Any();
                if (usernameExist)
                {
                    ModelState.AddModelError("UserName", "Username already taken. Please try again with other username.");
                }
                if (usernameExist)
                {
                    ModelState.AddModelError("Email", "Email Address already taken. Please try again with other Email Address.");
                }
            }
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Username, Email = model.Email, Name = model.Name, Gender = model.Gender };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Send an email with this link
                    string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    ConfirmEmailTemplate confirmEmailTemplate = GetConfirmEmailTemplate(model, callbackUrl);            
                    SendEmail(model.Email, confirmEmailTemplate.Subject, confirmEmailTemplate.Body);
                    
                    return RedirectToAction("EmailSent", "Account");
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        public ActionResult MyProfile()
        {
            MyProfileViewModel myProfileViewModel = GetUserProfile();
            return View(myProfileViewModel);
        }

        //check if the selected file name already exists in the system and return error message (triggered when user select file to upload as profile picture)
        public string ValidateUpload(string currentFileName)
        {
            List<string> names = new List<string>();
            names = db.AspNetUsers.Select(a => a.ProfilePicture).ToList();
            string errorMsg = "";
            if (names.Contains(currentFileName))
            {
                errorMsg = "The file " + currentFileName + " already exists in the system. Please rename your file and try again.";
                return errorMsg;
            }
            return null;
        }

        public ActionResult EditProfile()
        {
            MyProfileViewModel myProfileViewModel = GetUserProfile();
            return View(myProfileViewModel);
        }

        [HttpPost]
        public async Task<ActionResult> EditProfile(MyProfileViewModel model, IEnumerable<HttpPostedFileBase> ProfilePicture)
        {
            if (model != null)
            {
                AspNetUsers aspNetUsers = db.AspNetUsers.Find(model.Id);
                string originalUsername = aspNetUsers.UserName;
                string originalEmail = aspNetUsers.Email;
                //check whether username n email exists
                bool usernameExist = db.AspNetUsers.Where(a => a.UserName == model.Username && a.UserName != originalUsername).Any();
                bool emailExist = db.AspNetUsers.Where(a => a.Email == model.Email && a.Email != originalEmail).Any();
                if (usernameExist)
                {
                    ModelState.AddModelError("Username", "Username already exists. Please try with other username.");
                }
                if (emailExist)
                {
                    ModelState.AddModelError("Email", "Email already exists. Please try with other email address.");
                }
                if (ModelState.IsValid)
                {
                    try
                    {
                        string FileName = "";
                        aspNetUsers.UserName = model.Username;
                        aspNetUsers.Email = model.Email;
                        aspNetUsers.Name = model.Name;
                        aspNetUsers.Gender = model.Gender;
                        if (model.Email != originalEmail)
                        {
                            aspNetUsers.EmailConfirmed = false; //user has changed the email, set emailConfirmed to false, to allow user to confirm their email once again
                        }
                        if (ProfilePicture != null)
                        {
                            foreach (var file in ProfilePicture)
                            {
                                if (file != null)
                                {
                                    // get the file name of the selected image file and save into images folder
                                    FileName = new FileInfo(file.FileName).Name;
                                    string serverFilePath = HttpContext.Server.MapPath("~/Images/") + FileName;
                                    file.SaveAs(serverFilePath); //remember to set 'write' permission on Images folder in the server 
                                    aspNetUsers.ProfilePicturePath = serverFilePath;
                                }
                            }
                            aspNetUsers.ProfilePicture = FileName;
                        }
                        db.Entry(aspNetUsers).State = EntityState.Modified;
                        db.SaveChanges();
                        ModelState.Clear();
                        if (model.Email != originalEmail)
                        {
                            //send confirm account email
                            string code = await UserManager.GenerateEmailConfirmationTokenAsync(aspNetUsers.Id);
                            var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = aspNetUsers.Id, code = code }, protocol: Request.Url.Scheme);
                            ConfirmEmailTemplate confirmEmailTemplate = GetConfirmEmailChangingTemplate(model, callbackUrl);
                            SendEmail(model.Email, confirmEmailTemplate.Subject, confirmEmailTemplate.Body);
                            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                            return RedirectToAction("EmailSent", "Account");
                        }
                        if (model.Username != originalUsername)
                        {
                            TempData["ProfileUpdateSuccess"] = "Profile updated successfully. Please login.";
                            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                            return RedirectToAction("Login");
                        }
                        else
                        {
                            TempData["ProfileUpdateSuccess"] = "Profile updated successfully.";
                            return RedirectToAction("MyProfile");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("Id", ex.InnerException.Message);
                    }
                }
            }
            return View(model);
        }

        public MyProfileViewModel GetUserProfile()
        {
            MyProfileViewModel myProfileViewModel = new MyProfileViewModel();
            string userId = System.Web.HttpContext.Current.User.Identity.GetUserId();
            AspNetUsers aspNetUsers = db.AspNetUsers.Find(userId);
            myProfileViewModel.Id = aspNetUsers.Id;
            myProfileViewModel.Email = aspNetUsers.Email == null ? "" : aspNetUsers.Email;
            myProfileViewModel.Gender = aspNetUsers.Gender == null ? "" : aspNetUsers.Gender;
            myProfileViewModel.Name = aspNetUsers.Name == null ? "" : aspNetUsers.Name;
            myProfileViewModel.Username = aspNetUsers.UserName == null ? "" : aspNetUsers.UserName;
            myProfileViewModel.ProfilePicture = aspNetUsers.ProfilePicture == null ? "" : aspNetUsers.ProfilePicture;
            myProfileViewModel.ProfilePicturePath = aspNetUsers.ProfilePicturePath == null ? "" : aspNetUsers.ProfilePicturePath;
            return myProfileViewModel;
        }

        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                TempData["PasswordUpdateSuccess"] = "Password changed successfully.";
                return RedirectToAction("ChangePassword");
            }
            AddErrors(result);
            return View(model);
        }


        [AllowAnonymous]
        public ActionResult EmailSent()
        {
            return View();
        }

        public void SendEmail(string email, string subject, string body)
        {          
            string host = generalFunction.GetAppSettingsValue("smtpHost");
            string strPort = generalFunction.GetAppSettingsValue("smtpPort");
            int port = Int32.Parse(strPort);
            string userName = generalFunction.GetAppSettingsValue("smtpUserName");
            string password = generalFunction.GetAppSettingsValue("smtpPassword");
            var client = new SmtpClient(host, port);
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(userName, password);
            client.EnableSsl = true;
           
            MailMessage mail = new MailMessage(userName, email, subject, body);
            mail.IsBodyHtml = true;
            client.Send(mail);
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            AspNetUsers aspNetUsers = db.AspNetUsers.Find(userId);
            aspNetUsers.EmailConfirmed = true;
            db.Entry(aspNetUsers).State = EntityState.Modified;
            db.SaveChanges();
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }
        public ForgotPasswordEmailTemplate GetForgotPasswordEmailTemplate(ForgotPasswordViewModel model, string callbackUrl)
        {
            string username = db.AspNetUsers.Where(a => a.Email == model.Email).Select(a => a.UserName).DefaultIfEmpty("").FirstOrDefault();
            string websiteName = generalFunction.GetAppSettingsValue("websiteName");
            string subject = "Reset Password for " + websiteName;
            var link = "<a href='" + callbackUrl + "'>here</a>";
            string body = "<p>Hi " + username + ",<br /><br /> We got a request to reset your password for " + websiteName + ".</p><p>If it is true, Click " + link + " to reset your password.</p>" +
                        "<p>If you did not do so, please ignore this email.</p>" +
                        "<p><i>Do not reply to this email.</i></p><br /><br /><p>Regards,<br>" + websiteName + "</p>";
            ForgotPasswordEmailTemplate forgotPasswordEmail = new ForgotPasswordEmailTemplate();
            forgotPasswordEmail.Subject = subject;
            forgotPasswordEmail.Body = body;
            return forgotPasswordEmail;
        }

        public ConfirmEmailTemplate GetConfirmEmailTemplate(RegisterViewModel model, string callbackUrl)
        {
            string websiteName = generalFunction.GetAppSettingsValue("websiteName");
            var link = "<a href='" + callbackUrl + "'>here</a>";
            string body = "<p>Hi " + model.Username + ",<br /><br /> Thanks for signing up an account in " + websiteName + ".</p><p>Please confirm your account by clicking <a href='" + callbackUrl + "'> here</a></p>" +
                        "<p>If you did not do so, please ignore this email.</p>" +
                        "<p><i>Do not reply to this email.</i></p><br /><br /><p>Regards,<br>" + websiteName + "</p>";
            string subject = "Confirm email for sign up account in " + websiteName;

            ConfirmEmailTemplate confirmEmailTemplate = new ConfirmEmailTemplate();
            confirmEmailTemplate.Subject = subject;
            confirmEmailTemplate.Body = body;
            return confirmEmailTemplate;
        }

        public ConfirmEmailTemplate GetConfirmEmailChangingTemplate(MyProfileViewModel model, string callbackUrl)
        {
            string websiteName = generalFunction.GetAppSettingsValue("websiteName");
            var link = "<a href='" + callbackUrl + "'>here</a>";
            string body = "<p>Hi " + model.Username + ",<br /><br /> We have received a request to change your email address for your account in " + websiteName + ".</p><p>Please confirm your account by clicking <a href='" + callbackUrl + "'> here</a></p>" +
                        "<p>If you did not do so, please ignore this email.</p>" +
                        "<p><i>Do not reply to this email.</i></p><br /><br /><p>Regards,<br>" + websiteName + "</p>";
            string subject = "Confirm Email for Changing Email Address of an Account in " + websiteName;

            ConfirmEmailTemplate confirmEmailTemplate = new ConfirmEmailTemplate();
            confirmEmailTemplate.Subject = subject;
            confirmEmailTemplate.Body = body;
            return confirmEmailTemplate;
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var username = db.AspNetUsers.Where(a => a.Email == model.Email).Select(a => a.UserName).DefaultIfEmpty("").FirstOrDefault();
                var user = await UserManager.FindByNameAsync(username);
                //if user not exists or the email is not yet confirmed, go to ForgotPasswordConfirmation page instead of showing detail error message, to ensure account security
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return View("ForgotPasswordConfirmation");
                }
                string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                ForgotPasswordEmailTemplate forgotPasswordEmailTemplate = GetForgotPasswordEmailTemplate(model, callbackUrl);
                SendEmail(model.Email, forgotPasswordEmailTemplate.Subject, forgotPasswordEmailTemplate.Body);
                return RedirectToAction("ForgotPasswordConfirmation", "Account");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var username = db.AspNetUsers.Where(a => a.Email == model.Email).Select(a => a.UserName).DefaultIfEmpty("").FirstOrDefault();
            var user = await UserManager.FindByNameAsync(username);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
            return View();
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }
      
        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Home");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}