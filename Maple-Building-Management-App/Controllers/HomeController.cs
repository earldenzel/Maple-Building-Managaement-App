﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Maple_Building_Management_App.Models;
using DataLibrary;
using static DataLibrary.Logic.AccountProcessor;
using System.Threading.Tasks;
using static DataLibrary.Logic.ComplaintProcessor;
using System.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Twilio.TwiML;
using Twilio.AspNet.Mvc;

namespace Maple_Building_Management_App.Controllers
{
    public class HomeController : TwilioController
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult ViewAccounts()
        {
            ViewBag.Message = "Accounts List";

            var data = LoadAccounts();

            List<AccountModel> accounts = new List<AccountModel>();

            foreach (var row in data)
            {
                accounts.Add(new AccountModel
                {
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    EmailAddress = row.EmailAddress,
                    Tenant = row.Tenant,
                    PropertyCode = row.PropertyCode
                });
            }
            return View(accounts);
        }

        public ActionResult Register()
        {
            ViewBag.Message = "App Registration";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(AccountModel model)
        {
            if (ModelState.IsValid)
            {
                int recordsCreated = CreateAccount(
                    model.FirstName, 
                    model.LastName, 
                    model.EmailAddress, 
                    model.Password,
                    model.Tenant, 
                    model.PropertyCode);
                return View("SuccessfulRegister", model);
            }

            return View();
        }
        
        [AllowAnonymous]
        public ActionResult Login()
        {
            ViewBag.Message = "App Login";

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                List<DataLibrary.Models.AccountModel> accountModel = SearchAccount(
                    model.EmailAddress,
                    model.Password
                    );
                bool matchingFound = accountModel.Count > 0;

                if (matchingFound)
                {
                    DataLibrary.Models.AccountModel dbModel = accountModel.First();
                    if (dbModel.TwoFactor)
                    {
                        Session["UserID"] = dbModel.Id;
                        Session["Phone"] = dbModel.PhoneNumber;
                        return RedirectToAction("VerifyAccount");
                    }
                    else
                    {
                        Session["User"] = dbModel;
                        return RedirectToAction("ContentPage");
                    }
                }
                else
                {
                    ViewBag.ErrorMessage = "Login Failed";
                    return View();
                }
            }
            else
            {
                ViewBag.ErrorMessage = "Login Failed";
                return View();
            }
        }

        public ActionResult ContentPage()
        {
            return View();
        }

        public ActionResult FileComplaint()
        {
            ComplaintModel model = new ComplaintModel();
            model.IncidentDate = DateTime.Today;
            model.ComplaintStatus = ComplaintStatus.Open.ToString();
            ViewBag.Message = "Create Complaint";

            Session["TenantID"] = 1;
            Session["PropertyID"] = 2;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FileComplaint(ComplaintModel model)
        {
            if (ModelState.IsValid)
            {
                int recordsCreated = CreateComplaint(
                    (int)Session["TenantID"],
                    (int)Session["PropertyID"],
                    model.IncidentDate,
                    model.Description,
                    (int) Enum.Parse(typeof(ComplaintStatus), model.ComplaintStatus),
                    (int) Enum.Parse(typeof(ComplaintType), model.ComplaintType)
                );
                return View("SuccessfulComplaint", model);
            }

            return View();
        }

        public ActionResult VerifyAccount()
        {
            if (Session["SentMessageLogin"] == null || Session["ExpectedCodeLogin"] == null)
            {
                var accountSid = ConfigurationManager.AppSettings["SMSAccountIdentification"];
                var authToken = ConfigurationManager.AppSettings["SMSAccountPassword"];
                TwilioClient.Init(accountSid, authToken);


                var to = new PhoneNumber(Session["Phone"].ToString());
                var from = new PhoneNumber(ConfigurationManager.AppSettings["SMSAccountFrom"]);

                Random generator = new Random();
                string r = generator.Next(0, 999999).ToString("D6");
                Session["ExpectedCodeLogin"] = r;

                var message = MessageResource.Create(
                    to: to,
                    from: from,
                    body: "Your verification code for the Maple Building Management App is " + r);

                Session["SentMessageLogin"] = message.Sid;
            }
            return View();
        }

        public ActionResult ViewComplaints()
        {
            int preview_max = 15;
            var data = LoadComplaints();
            List<ComplaintModel> complaints = new List<ComplaintModel>();

            foreach (var row in data)
            {
                string preview = row.Details.Substring(0, Math.Min(preview_max, row.Details.Length));
                if (row.Details.Length > preview_max)
                {
                    preview += "...";
                }

                complaints.Add(new ComplaintModel
                {
                    Id = row.Id,
                    ComplaintStatus = Enum.GetName(typeof(ComplaintStatus), row.ComplaintStatusId),
                    ComplaintType = Enum.GetName(typeof(ComplaintType), row.ComplaintTypeId),
                    Description = preview,
                    IncidentDate = row.IncidentDate
                });
            }

            return View(complaints);
        }

        public ActionResult ComplaintDetails(int id)
        {
            var data = LoadComplaint(id).FirstOrDefault();
            ComplaintModel complaint = new ComplaintModel();

            complaint.Id = data.Id;
            complaint.ComplaintStatus = Enum.GetName(typeof(ComplaintStatus), data.ComplaintStatusId);
            complaint.ComplaintType = Enum.GetName(typeof(ComplaintType), data.ComplaintTypeId);
            complaint.Description = data.Details;
            complaint.IncidentDate = data.IncidentDate;

            return View(complaint);
        }

        public ActionResult EditComplaint(int id)
        {
            var data = LoadComplaint(id).FirstOrDefault();
            ComplaintModel complaint = new ComplaintModel();

            complaint.Id = data.Id;
            complaint.ComplaintStatus = Enum.GetName(typeof(ComplaintStatus), data.ComplaintStatusId);
            complaint.ComplaintType = Enum.GetName(typeof(ComplaintType), data.ComplaintTypeId);
            complaint.Description = data.Details;
            complaint.IncidentDate = data.IncidentDate;

            return View(complaint);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyAccount(VerifyModel model)
        {
            if (ModelState.IsValid)
            {
                if (Session["SentMessageLogin"] == null || Session["ExpectedCodeLogin"] == null)
                {
                    TempData["Error"] = "Message has timed out. Please try to log in again";
                    return RedirectToAction("Index");
                }
                else if (string.Equals(model.VerificationCode, Session["ExpectedCodeLogin"].ToString()))
                {
                    Session["User"] = SearchAccount((int)Session["UserID"]);
                    Session.Remove("UserID");
                    Session.Remove("SentMessageLogin");
                    Session.Remove("ExpectedCodeLogin");
                    return RedirectToAction("ContentPage");
                }
                else
                {
                    ViewBag.ErrorMessage = "Please input the correct verification code!";
                }
            }
            return View();
        }

        public ActionResult Logout()
        {
            Session.Remove("User");
            return RedirectToAction("Index");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditComplaint(ComplaintModel model)
        {
            if (ModelState.IsValid)
            {
                int recordUpdated = UpdateComplaint(
                    model.Id,
                    model.IncidentDate,
                    model.Description,
                    (int)Enum.Parse(typeof(ComplaintStatus), model.ComplaintStatus),
                    (int)Enum.Parse(typeof(ComplaintType), model.ComplaintType)
                );
                return RedirectToAction("ViewComplaints");
            }

            return View();
        }

        public ActionResult DeleteComplaint(int id)
        {
            int recordDeleted = DeleteComplaintData(id);

            return RedirectToAction("ViewComplaints");
        }

        public ViewResult ComplainList()
        {
            return View();
        }

        [HttpGet]
        public ActionResult ViewProfile()
        {
            var data = LoadAccounts().FirstOrDefault();
            AccountModel profile = new AccountModel();

            profile.FirstName = data.FirstName;
            profile.LastName = data.LastName;
            profile.EmailAddress = data.EmailAddress;
            profile.Tenant = data.Tenant;
            profile.PropertyCode = data.PropertyCode;

            return View(profile);
        }

        [HttpGet]
        public ActionResult EditProfile()
        {
            var data = LoadAccounts().FirstOrDefault();
            AccountModel profile = new AccountModel();

            profile.FirstName = data.FirstName;
            profile.LastName = data.LastName;
            profile.EmailAddress = data.EmailAddress;
            profile.Tenant = data.Tenant;
            profile.PropertyCode = data.PropertyCode;

            return View(profile);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult EditProfile(AccountModel profile)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        int recordsCreated = UpdateProfile(
        //            profile.FirstName,
        //            profile.LastName,
        //            profile.EmailAddress,
        //            profile.Tenant,
        //            profile.PropertyCode);

        //        return RedirectToAction("ViewProfile");
        //    }
        //    return View();
        //}
    }
}