using Bulky.DataAccess.Data;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace bulkyApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.AdminRole)]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public UserController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
            _db = db;
        }


        public IActionResult Index()
        {
            return View();
        }

        public IActionResult RoleManagement(string userId)
        {
            string RoleID = _db.UserRoles.FirstOrDefault(u => u.UserId == userId).RoleId;

            RoleManagementVM RoleVM = new RoleManagementVM()
            {
                applicationUser = _db.ApplicationUsers.Include(u => u.Company).FirstOrDefault(u => u.Id == userId),
                RoleList = _db.Roles.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Name
                }),


                CompanyList = _db.companies.Select(c => new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                })

            };

            RoleVM.applicationUser.Role = _db.Roles.FirstOrDefault(u => u.Id == RoleID).Name;

            return View(RoleVM);
        }

        [HttpPost]
        public IActionResult RoleManagement(RoleManagementVM roleManagementVM)
        {
            // Fetch user first
            ApplicationUser applicationUser = _db.ApplicationUsers.FirstOrDefault(u => u.Id == roleManagementVM.applicationUser.Id);
            if (applicationUser == null) return View("Index");

            string RoleID = _db.UserRoles.FirstOrDefault(u => u.UserId == roleManagementVM.applicationUser.Id).RoleId;
            string oldRole = _db.Roles.FirstOrDefault(u => u.Id == RoleID).Name;

            if (!(roleManagementVM.applicationUser.Role == oldRole))
            {
                if (roleManagementVM.applicationUser.Role == SD.CompanyRole)
                {
                    applicationUser.CompanyId = roleManagementVM.applicationUser.CompanyId;
                }
                if (oldRole == SD.CompanyRole)
                {
                    applicationUser.CompanyId = null;
                }

                _db.SaveChanges();

                _userManager.RemoveFromRoleAsync(applicationUser, oldRole).GetAwaiter().GetResult();
                _userManager.AddToRoleAsync(applicationUser, roleManagementVM.applicationUser.Role).GetAwaiter().GetResult();

            }
            else
            {
                if (oldRole == SD.CompanyRole && applicationUser.CompanyId != roleManagementVM.applicationUser.CompanyId)
                {
                    applicationUser.CompanyId = roleManagementVM.applicationUser.CompanyId;
                    _db.ApplicationUsers.Update(applicationUser);
                    _db.SaveChanges();
                }
            }

            return View("Index");
        }




        #region API CALLS

        [HttpGet]
        public IActionResult GetAll()
        {



            List<ApplicationUser> objuserlist = _db.ApplicationUsers.Include(u => u.Company).ToList();

            var userRole = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();


            foreach (var user in objuserlist)
            {

                var RoleId = userRole.FirstOrDefault(u => u.UserId == user.Id).RoleId;
                user.Role = roles.FirstOrDefault(u => u.Id == RoleId).Name;

                if (user.Company == null)
                {
                    user.Company = new()
                    {
                        Name = ""
                    };
                }
            }

            return Json(new { data = objuserlist });
        }

        [HttpPost]
        public IActionResult LockUnlock([FromBody] string id)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);

            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while locking/unlocking" });
            }

            // Get user role
            var userRole = _db.UserRoles.FirstOrDefault(u => u.UserId == id);
            string roleName = null;
            if (userRole != null)
            {
                var role = _db.Roles.FirstOrDefault(r => r.Id == userRole.RoleId);
                roleName = role?.Name;
            }

            // If user is Admin, prevent locking
            if (roleName == "Admin")
            {
                return Json(new { success = false, message = "The Admin cannot be locked" });
            }

            // Toggle lock/unlock
            if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
            {

                objFromDb.LockoutEnd = DateTime.Now;
            }
            else
            {

                objFromDb.LockoutEnd = DateTime.Now.AddYears(1000);
            }

            _db.SaveChanges();

            return Json(new { success = true, message = "User locked/unlocked successfully" });
        }

        #endregion
    }
}
