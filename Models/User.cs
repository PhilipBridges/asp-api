using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TodoApi.Models
{
    public class User
    {
        public long Id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string password { get; set; }

    }
}
