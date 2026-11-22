using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Web.Models;
using LibraryManagement.Web.Services;
using LibraryManagement.Web.ViewModels.Categories;

namespace LibraryManagement.Web.Controllers;

/// <summary>
/// UC05: Quản lý thể loại sách (Member B).
/// </summary>
[Authorize(Roles = nameof(Role.Librarian))]
public class CategoriesController : Controller
{
    private readonly CategoryService _categoryService;

    public CategoriesController(CategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    // GET: /Categories
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var list = await _categoryService.GetAllWithBookCountAsync();
        return View(list);
    }

    // GET: /Categories/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CategoryFormVM());
    }

    // POST: /Categories/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormVM model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _categoryService.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // GET: /Categories/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var cat = await _categoryService.GetByIdAsync(id);
        if (cat == null)
        {
            TempData["Error"] = "Không tìm thấy thể loại.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new CategoryFormVM
        {
            Id = cat.Id,
            Name = cat.Name,
            Description = cat.Description
        };
        return View(vm);
    }

    // POST: /Categories/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, CategoryFormVM model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _categoryService.UpdateAsync(id, model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Message!);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // POST: /Categories/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _categoryService.DeleteAsync(id);
        if (result.Success)
            TempData["Success"] = result.Message;
        else
            TempData["Error"] = result.Message;

        return RedirectToAction(nameof(Index));
    }
}

