using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using TrackerEnabledDbContext;

namespace SimpleAuthentication.Models
{
    public class DbContext
    {

    }

    public class DefaultContext : TrackerContext
    {
        public DefaultContext()
            : base("DefaultConnection")
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }

        public DbSet<AspNetUsers> AspNetUsers { get; set; }
        public DbSet<AspNetUserClaims> AspNetUserClaims { get; set; }
        public DbSet<AspNetUserLogins> AspNetUserLogins { get; set; }
        public DbSet<AspNetRoles> AspNetRoles { get; set; }
        public DbSet<AspNetUserRoles> AspNetUserRoles { get; set; }

    }
}