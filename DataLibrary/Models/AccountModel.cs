﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLibrary.Models
{
    public class AccountModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public string Password { get; set; }
        public bool Tenant { get; set; }
        public string PropertyCode { get; set; }
        public bool TwoFactor { get; set; }
        public string PhoneNumber { get; set; }
        public bool Admin { get; set; }
    }
}
