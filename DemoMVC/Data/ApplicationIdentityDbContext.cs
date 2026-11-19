using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public sealed class ApplicationIdentityDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationIdentityDbContext(
            DbContextOptions<ApplicationIdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasAnnotation("DemoMVC:StudentOwnershipColumn", "UserId");
            builder.HasAnnotation("DemoMVC:StudentApprovalColumn", "IsApproved");
            builder.HasAnnotation("DemoMVC:StudentApprovalTrigger", "UserId");
        }
    }
}
