using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace bulkyApp.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {


        private readonly IUnitOfWork _unitofwork;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitofwork)
        {
            _unitofwork = unitofwork;

        }

        public IActionResult Index()
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;


            ShoppingCartVM = new()
            {

                ShoppingCardList = _unitofwork.ShoppingCard.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                orderHeader = new()


            };



            foreach (var cart in ShoppingCartVM.ShoppingCardList)
            {
                cart.Price = GetPricebaseOnQuantity(cart);
                ShoppingCartVM.orderHeader.OrderTotal += (int)(cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }


        private double GetPricebaseOnQuantity(ShoppingCard shoppingCard)
        {

            if (shoppingCard.Count <= 50)
            {
                return shoppingCard.Product.ListPrice;
            }
            else if (shoppingCard.Count >= 50)
            {
                return shoppingCard.Product.Price50;
            }
            else
            {
                return shoppingCard.Product.Price100;
            }


        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitofwork.ShoppingCard.Get(u => u.Id == cartId);
            cartFromDb.Count++;
            _unitofwork.ShoppingCard.Update(cartFromDb);
            _unitofwork.Save();
            return RedirectToAction(nameof(Index));

        }


        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitofwork.ShoppingCard.Get(u => u.Id == cartId, tracked: true);

            if (cartFromDb.Count <= 1)
            {
                // Remove

                HttpContext.Session.SetInt32(SD.SessionCart, _unitofwork.ShoppingCard.GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
                _unitofwork.ShoppingCard.Remove(cartFromDb);

            }
            else
            {
                cartFromDb.Count--;
                _unitofwork.ShoppingCard.Update(cartFromDb);
            }

            _unitofwork.Save();
            return RedirectToAction(nameof(Index));

        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitofwork.ShoppingCard.Get(u => u.Id == cartId, tracked: true);


            _unitofwork.ShoppingCard.Remove(cartFromDb);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitofwork.ShoppingCard.GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            _unitofwork.Save();
            return RedirectToAction(nameof(Index));

        }

        public IActionResult Summary()
        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);


            ShoppingCartVM = new()
            {

                ShoppingCardList = _unitofwork.ShoppingCard.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                orderHeader = new()


            };

            ShoppingCartVM.orderHeader.applicationUser = _unitofwork.ApplicationUser.Get(u => u.Id == userId);

            ShoppingCartVM.orderHeader.Name = ShoppingCartVM.orderHeader.applicationUser.Name;
            ShoppingCartVM.orderHeader.phoneNumber = ShoppingCartVM.orderHeader.applicationUser.PhoneNumber;
            ShoppingCartVM.orderHeader.StreetAdress = ShoppingCartVM.orderHeader.applicationUser.StreetAddress;
            ShoppingCartVM.orderHeader.City = ShoppingCartVM.orderHeader.applicationUser.City;
            ShoppingCartVM.orderHeader.PostaCode = ShoppingCartVM.orderHeader.applicationUser.PostalCode;
            ShoppingCartVM.orderHeader.State = ShoppingCartVM.orderHeader.applicationUser.State;



            foreach (var cart in ShoppingCartVM.ShoppingCardList)
            {
                cart.Price = GetPricebaseOnQuantity(cart);
                ShoppingCartVM.orderHeader.OrderTotal += (int)(cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPost()
        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ShoppingCartVM.ShoppingCardList = _unitofwork.ShoppingCard.GetAll(u => u.ApplicationUserId == userId,
            includeProperties: "Product");

            ShoppingCartVM.orderHeader.OrderDate = System.DateTime.Now;
            ShoppingCartVM.orderHeader.ApplicationUserId = userId;

            ApplicationUser applicationUser = _unitofwork.ApplicationUser.Get(u => u.Id == userId);



            foreach (var cart in ShoppingCartVM.ShoppingCardList)
            {
                cart.Price = GetPricebaseOnQuantity(cart);
                ShoppingCartVM.orderHeader.OrderTotal += (int)(cart.Price * cart.Count);
            }

            if (applicationUser.CompanyId == null)
            {
                // its a costumer 
                ShoppingCartVM.orderHeader.PaymentStatus = SD.PaymentStatusPanding;
                ShoppingCartVM.orderHeader.OrderStatus = SD.StatusPanding;
            }
            else
            {
                // its a company 
                ShoppingCartVM.orderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.orderHeader.OrderStatus = SD.StatusApproved;
            }
            _unitofwork.OrderHeader.Add(ShoppingCartVM.orderHeader);
            _unitofwork.Save();

            foreach (var cart in ShoppingCartVM.ShoppingCardList)
            {
                OrderDetail orderDetail = new()
                {
                    productId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.orderHeader.Id,
                    price = cart.Price,
                    Count = cart.Count

                };

                _unitofwork.OrderDetail.Add(orderDetail);

            }

            _unitofwork.Save();


            if (applicationUser.CompanyId == null)
            {
                // strip logic
                var domain = "http://localhost:5067/";

                var options = new Stripe.Checkout.SessionCreateOptions
                {
                    SuccessUrl = domain + $"Customer/Cart/OrderConfirmation?id={ShoppingCartVM.orderHeader.Id}",
                    LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                    Mode = "payment",
                };

                foreach (var Item in ShoppingCartVM.ShoppingCardList)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {

                            UnitAmount = (long)(Item.Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = Item.Product.Title
                            }

                        },
                        Quantity = Item.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }

                var service = new Stripe.Checkout.SessionService();
                Stripe.Checkout.Session session = service.Create(options);
                _unitofwork.OrderHeader.UpdateStripePaymentId(
                 ShoppingCartVM.orderHeader.Id, session.Id, session.PaymentIntentId);
                _unitofwork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);

            }

            return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.orderHeader.Id });
        }

        public IActionResult OrderConfirmation(int id)
        {

            OrderHeader orderHeader = _unitofwork.OrderHeader.Get(x => x.Id == id, includeProperties: "applicationUser");
            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitofwork.OrderHeader.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
                    _unitofwork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitofwork.Save();

                }

                List<ShoppingCard> shoppingCards = _unitofwork.ShoppingCard
                .GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();


                _unitofwork.ShoppingCard.RemoveRange(shoppingCards);
                _unitofwork.Save();
            }
            return View(id);
        }
    }
}
