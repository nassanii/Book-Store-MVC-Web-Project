using Bulky.DataAccess.Data;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bulky.DataAccess.DbInitailizer
{
    public class DbInitailizer : IDbInitailizer
    {

        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;


        public DbInitailizer(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        public void initialize()
        {

            // migrations if there are not applaied
            try
            {
                if (_db.Database.GetPendingMigrations().Count() > 0)
                {
                    _db.Database.Migrate();
                }
            }
            catch (Exception ex) { }

            // Create Roles if there are not created 

            if (!_roleManager.RoleExistsAsync(SD.CustomerRole).GetAwaiter().GetResult())
            {
                _roleManager.CreateAsync(new IdentityRole(SD.CustomerRole)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.EmployeeRole)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.AdminRole)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.CompanyRole)).GetAwaiter().GetResult();



                // if the role not created it will create a admin user as will 
                _userManager.CreateAsync(new ApplicationUser
                {

                    UserName = "admin@nassanidev.com",
                    Email = "admin@nassanidev.com",
                    Name = "NassaniDev",
                    PhoneNumber = "05538866317",
                    StreetAddress = "Sehit Ferhat",
                    State = "GA",
                    City = "Gaziantep",
                    PostalCode = "2700"


                }, "Admin123!").GetAwaiter().GetResult();

                ApplicationUser user = _db.ApplicationUsers.FirstOrDefault(u => u.Email == "admin@nassanidev.com");
                _userManager.AddToRoleAsync(user, SD.AdminRole).GetAwaiter().GetResult();

            }




            return;
        }
    }
}
