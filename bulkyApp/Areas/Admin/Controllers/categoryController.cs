using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bulkyApp.Areas.Admin.Controllers
{
    // This controller is used to manage categories
    [Area("Admin")]
    [Authorize(Roles = SD.AdminRole)]
    public class CategoryController : Controller
    {
        // Injecting the ApplicationDbContext to interact with the database
        private readonly IUnitOfWork _unitofwork;
        public CategoryController(IUnitOfWork unitOfWork)
        {
            _unitofwork = unitOfWork;
        }

        // This method is used to display the list of categories
        public IActionResult Index()
        {
            List<Category> objCategoryList = _unitofwork.Category.GetAll().ToList();

            return View(objCategoryList);
        }


        // This method is used to create a category
        public IActionResult Create()

        {

            return View();
        }
        // This method is used to save a new category
        [HttpPost]
        public IActionResult Create(Category obj)
        {
            if (obj.Name == obj.DisplayOrder.ToString())
            {
                ModelState.AddModelError("name", "The display order can not be exactly match the Name.");
            }
            // Check if Name is an integer
            if (int.TryParse(obj.Name, out _)) // If Name can be parsed as an integer
            {
                ModelState.AddModelError("name", "Name cannot be a number only.");
            }

            if (ModelState.IsValid)
            {
                _unitofwork.Category.Add(obj);
                _unitofwork.Save(); // Save changes to the database
                TempData["success"] = "Category created successfully";
                return RedirectToAction("Index");
            }
            return View(obj);

        }


        // This method is used to Edit a category
        public IActionResult Edit(int? id)

        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            Category categoryFromDB = _unitofwork.Category.Get(u => u.Id == id);
            if (categoryFromDB == null)
            {
                return NotFound();
            }
            return View(categoryFromDB);
        }

        // This method is used to update a category
        [HttpPost]
        public IActionResult Edit(Category obj)

        {

            if (ModelState.IsValid)
            {
                _unitofwork.Category.Update(obj); // Update the category in the database
                _unitofwork.Save(); // Save changes to the database
                TempData["success"] = "Category Edited successfully";
                return RedirectToAction("Index");
            }
            return View(obj);

        }


        public IActionResult Delete(int? id)

        {
            if (id == null || id == 0)
            {
                return NotFound();
            }

            Category categoryFromDB = _unitofwork.Category.Get(u => u.Id == id);
            // Check if the category exists in the database
            if (categoryFromDB == null)
            {
                return NotFound();
            }
            return View(categoryFromDB);
        }

        // This method is used to update a category
        [HttpPost, ActionName("Delete")]
        public IActionResult DeletePost(int? id)

        {
            Category? obj = _unitofwork.Category.Get(u => u.Id == id);
            if (obj == null)
            {
                return NotFound();
            }
            _unitofwork.Category.Remove(obj); // Use category repository to remove the category
            _unitofwork.Save(); // Save changes to the database
            TempData["success"] = "Category Deleted successfully";

            return RedirectToAction("Index");



        }

    }
}
