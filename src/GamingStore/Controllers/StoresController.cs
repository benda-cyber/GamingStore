using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GamingStore.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GamingStore.Data;
using GamingStore.Models;
using GamingStore.Models.Relationships;
using GamingStore.ViewModels;
using GamingStore.ViewModels.Stores;
using LoggerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Vereyon.Web;

namespace GamingStore.Controllers
{
    public class StoresController : BaseController
    {
        private readonly IFlashMessage _flashMessage;
        private readonly ILoggerManager _logger;

        public StoresController(UserManager<Customer> userManager, StoreContext context, RoleManager<IdentityRole> roleManager, SignInManager<Customer> signInManager,IFlashMessage flashMessage, ILoggerManager logger)
            : base(userManager, context, roleManager, signInManager)
        {
            _flashMessage = flashMessage;
            _logger = logger;
        }

        // GET: Stores
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            List<Store> stores = await Context.Stores.ToListAsync();
            HashSet<string> uniqueCities = GetUniqueCities(stores);
            List<Store> openStores = GetOpenStores(stores);


            var viewModel = new StoresCitiesViewModel
            {
                Stores = stores,
                CitiesWithStores = uniqueCities.ToArray(),
                OpenStores = openStores,
                ItemsInCart = await CountItemsInCart()
            };

            return View(viewModel);
        }

        private static List<Store> GetOpenStores(List<Store> stores)
        {
            List<Store> openStores = stores.Where(store => store.IsOpen()).ToList(); // get open stores

            return openStores;
        }

        // Post: Stores
        [HttpPost]
        public async Task<IActionResult> Index(StoresCitiesViewModel received)
        {
            List<Store> stores = await Context.Stores.ToListAsync();
            HashSet<string> uniqueCities = GetUniqueCities(stores);

            // get open stores
            List<Store> openStores = stores.Where(store => store.IsOpen()).ToList();
            
            if (!string.IsNullOrEmpty(received.Name))
            {
                stores = stores.Where(store => store.Name.ToLower().Contains(received.Name.ToLower())).ToList();
            }

            if (!string.IsNullOrEmpty(received.City))
            {
                stores = stores.Where(store => store.Address.City == received.City).ToList();
            }

            if (received.IsOpen)
            {
                stores = stores.Where(store => store.IsOpen()).ToList();
            }

            var viewModel = new StoresCitiesViewModel
            {
                Stores = stores,
                CitiesWithStores = uniqueCities.ToArray(),
                OpenStores = openStores,
                Name = received.Name,
                City = received.City,
                IsOpen = received.IsOpen,
                ItemsInCart = await CountItemsInCart()
            };

            return View(viewModel);
        }


        private static HashSet<string> GetUniqueCities(List<Store> stores)
        {
            // get stores with cities uniquely 
            var uniqueCities = new HashSet<string>();
            
            foreach (Store element in stores)
            {
                uniqueCities.Add(element.Address.City);
            }

            return uniqueCities;
        }


        // GET: Stores/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Store store = await Context.Stores.FirstOrDefaultAsync(m => m.Id == id);
            if (store == null)
            {
                return NotFound();
            }

            var viewModel = new StoreDetailsViewModel()
            {
                Store = store,
                ItemsInCart = await CountItemsInCart()
            };

            return View(viewModel);
        }

        // GET: Stores/Create
        [Authorize(Roles = "Admin,Viewer")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateStoreViewModel()
            {
                Store = new Store()
                {
                    Active = true,
                    OpeningHours = new List<OpeningHours>(7)
                    {
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Sunday},
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Monday},
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Tuesday},
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Wednesday},
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Thursday},
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Friday},
                        new OpeningHours() {OpeningTime = new TimeSpan(00, 00, 00), ClosingTime = new TimeSpan(23, 59, 00), DayOfWeek = DayOfWeek.Saturday}
                    },
                    Address = new Address()
                    {
                        Country = "Israel"
                    }
                }
            };

            return View(viewModel);
        }

        // POST: Stores/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Viewer")]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,PhoneNumber,Email,OpeningHours")] Store store)
        {
            var viewModel = new CreateStoreViewModel()
            {
                Store = store,
            };

            if (store.OpeningHours.Any(openingHour => openingHour.ClosingTime <= openingHour.OpeningTime))
            {
                _flashMessage.Danger("Store opening hours invalid. Store cannot be closed before it was opened");
                return RedirectToAction("ListStores", "Administration");
            }

            if (!ModelState.IsValid)
            {
                _flashMessage.Danger("Form is not valid");
                return View(viewModel);
            }

            Context.Add(store);
            await Context.SaveChangesAsync();

            return RedirectToAction("ListStores", "Administration");
        }

        // GET: Stores/Edit/5
        [Authorize(Roles = "Admin,Viewer")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            if (!StoreExists(id.Value))
            {
                _flashMessage.Danger("You cannot edit a store that is no longer exists");
                return RedirectToAction("ListStores", "Administration");
            }

            Store store = await Context.Stores.FindAsync(id);
            var viewModel = new StoreDetailsViewModel()
            {
                Store = store,
                ItemsInCart = await CountItemsInCart()
            };

            if (store == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        // POST: Stores/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Viewer")]
        public async Task<IActionResult> Edit(Store store)
        {
            if (!StoreExists(store.Id))
            {
                _flashMessage.Danger("You cannot edit a store that is no longer exists");
                return RedirectToAction("ListStores", "Administration");
            }

            // GetRolesAsync returns the list of user Roles
            IList<string> userRoles = await UserManager.GetRolesAsync(await GetCurrentUserAsync());

            if (!userRoles.Any(r => r.Equals("Admin")))
            {
                _flashMessage.Danger("Your changes were not saved.\n You do not have the right permissions to edit a stores.");
                return RedirectToAction("ListStores", "Administration");
            }


            if (store.OpeningHours.Any(openingHour => openingHour.ClosingTime <= openingHour.OpeningTime))
            {
                _flashMessage.Danger("Store opening hours invalid. Store cannot be closed before it was opened");

                return RedirectToAction("Edit", "Stores", new
                {
                    id = store.Id
                });
            }

            try
            {
                Context.Update(store);
                await Context.SaveChangesAsync();
                _flashMessage.Confirmation("Store information has been updated");
            }
            catch (Exception e)
            {
                _flashMessage.Danger("Store could not be updated");
                _logger.LogError($"Store# '{store.Id}' could not be updated, ex: {e}");
            }

            return RedirectToAction("ListStores", "Administration");
        }

        // GET: Stores/Delete/5
        [Authorize(Roles = "Admin,Viewer")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            if (!StoreExists(id.Value))
            {
                _flashMessage.Danger("You cannot remove a store that is no longer exists");
                return RedirectToAction("ListStores", "Administration");
            }

            Store store = await Context.Stores.FirstOrDefaultAsync(m => m.Id == id);
            var viewModel = new StoreDetailsViewModel()
            {
                Store = store, ItemsInCart = await CountItemsInCart()
            };
            if (store == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        // POST: Stores/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Viewer")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!StoreExists(id))
            {
                _flashMessage.Danger("You cannot remove a store that is no longer exists");
                return RedirectToAction("ListStores", "Administration");
            }

            // GetRolesAsync returns the list of user Roles
            IList<string> userRoles = await UserManager.GetRolesAsync(await GetCurrentUserAsync());

            if (!userRoles.Any(r => r.Equals("Admin")))
            {
                _flashMessage.Danger("Your changes were not saved.\n You do not have the right permissions to delete stores.");
                return RedirectToAction("ListStores", "Administration");
            }


            Store store = await Context.Stores.Include(s => s.Orders).FirstOrDefaultAsync(s => s.Id == id);

            if (store.Orders.Count > 0)
            {
                _flashMessage.Danger("You can not remove a store contains orders");
                return RedirectToAction("ListStores", "Administration");
            }

            Context.Stores.Remove(store);
            await Context.SaveChangesAsync();
            _flashMessage.Confirmation("Deleted success");

            return RedirectToAction("ListStores", "Administration");
        }

        private bool StoreExists(int id)
        {
            return Context.Stores.Any(e => e.Id == id);
        }
    }
}