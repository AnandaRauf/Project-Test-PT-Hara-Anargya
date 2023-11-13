using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace SimpleAuthentication.Models
{
    public class AspNetUserLogins
    {
        [Key, Column(Order = 1)]
        public string LoginProvider { get; set; }
        [Key, Column(Order = 2)]
        public string ProviderKey { get; set; }
        [Key, Column(Order = 3)]
        public string UserId { get; set; }
     
    }
}