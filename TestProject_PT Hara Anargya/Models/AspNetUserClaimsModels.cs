using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace SimpleAuthentication.Models
{
    public class AspNetUserClaims
    {
        [Key, Column(Order = 1)]
        public int Id { get; set; }
        [Key, Column(Order = 2)]
        public string UserId { get; set; }
        public string ClaimType { get; set; }
        public string ClaimValue { get; set; }
    }
}
