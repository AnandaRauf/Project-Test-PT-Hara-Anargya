using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace SimpleAuthentication.Models
{
    public class AspNetRoles
    {
        [Key]
        public string Id { get; set; }
        public string Name { get; set; }
       
    }
}