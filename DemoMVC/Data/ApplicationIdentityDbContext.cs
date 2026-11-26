using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public sealed class ApplicationIdentityDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public DbSet<RoleActionPermission> RoleActionPermissions => Set<RoleActionPermission>();

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

            builder.Entity<RoleActionPermission>(entity =>
            {
                entity.HasKey(permission => new
                {
                    permission.RoleId,
                    permission.Area,
                    permission.Controller,
                    permission.Action
                });
                entity.Property(permission => permission.RoleId).HasMaxLength(450);
                entity.Property(permission => permission.Area).HasMaxLength(128);
                entity.Property(permission => permission.Controller).HasMaxLength(128);
                entity.Property(permission => permission.Action).HasMaxLength(128);
                entity.HasOne<IdentityRole>()
                    .WithMany()
                    .HasForeignKey(permission => permission.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
