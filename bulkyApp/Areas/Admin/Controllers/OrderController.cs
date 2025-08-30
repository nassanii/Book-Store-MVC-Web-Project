using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
namespace bulkyApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/[controller]")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public OrderVM OrderVM { get; set; }
        public OrderController(IUnitOfWork unitofwork)
        {
            _unitOfWork = unitofwork;
        }

        // Index page
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        // Details page
        [HttpGet("Details/{orderId}")]
        public IActionResult Details(int orderId)
        {
            OrderVM = new()
            {
                OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, includeProperties: "applicationUser"),
                OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product")
            };

            return View(OrderVM);
        }


        [HttpPost("UpdateOrderDetails")]
        [Authorize(Roles = SD.AdminRole + "," + SD.EmployeeRole)]
        public IActionResult UpdateOrderDetails()

        {

            var orderHeaderFromDB = _unitOfWork.OrderHeader.Get(
                    u => u.Id == OrderVM.OrderHeader.Id
                );

            orderHeaderFromDB.Name = OrderVM.OrderHeader.Name;
            orderHeaderFromDB.phoneNumber = OrderVM.OrderHeader.phoneNumber;
            orderHeaderFromDB.StreetAdress = OrderVM.OrderHeader.StreetAdress;
            orderHeaderFromDB.City = OrderVM.OrderHeader.City;
            orderHeaderFromDB.State = OrderVM.OrderHeader.State;
            orderHeaderFromDB.PostaCode = OrderVM.OrderHeader.PostaCode;

            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
            {
                orderHeaderFromDB.Carrier = OrderVM.OrderHeader.Carrier;
            }
            if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
            {
                orderHeaderFromDB.Carrier = OrderVM.OrderHeader.TrackingNumber;
            }
            _unitOfWork.OrderHeader.Update(orderHeaderFromDB);
            _unitOfWork.Save();

            TempData["Success"] = "Order Deails Updated Successfully";

            return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDB.Id });
        }


        [HttpPost("StartProcessing")]
        [Authorize(Roles = SD.AdminRole + "," + SD.EmployeeRole)]
        public IActionResult StartProcessing()
        {
            _unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
            _unitOfWork.Save();
            TempData["Success"] = "Order Deails Updated Successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });

        }


        [HttpPost("ShipOrder")]
        [Authorize(Roles = SD.AdminRole + "," + SD.EmployeeRole)]
        public IActionResult ShipOrder()
        {
            if (string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
            {
                ModelState.AddModelError("OrderHeader.Carrier", "Carrier is required to ship the order.");
            }
            if (string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
            {
                ModelState.AddModelError("OrderHeader.TrackingNumber", "Tracking Number is required to ship the order.");
            }


            var orderheader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
            orderheader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
            orderheader.Carrier = OrderVM.OrderHeader.Carrier;
            orderheader.OrderStatus = SD.StatusShipped;
            orderheader.ShippingDate = DateTime.Now;
            if (orderheader.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                orderheader.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
            }

            _unitOfWork.OrderHeader.Update(orderheader);
            _unitOfWork.Save();

            _unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusShipped);
            _unitOfWork.Save();
            TempData["Success"] = "Order Deails Shipped Successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });

        }


        [HttpPost("CancelOrder")]
        [Authorize(Roles = SD.AdminRole + "," + SD.EmployeeRole)]
        public IActionResult CancelOrder()
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);

            if (orderHeader.PaymentStatus == SD.StatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = orderHeader.PaymentIntentId
                };

                var Service = new RefundService();
                Refund refund = Service.Create(options);

                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefounded);
            }
            else
            {
                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
            }

            _unitOfWork.Save();
            TempData["Success"] = "Order Deails Canceld Successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }


        [ActionName("PayNow")]
        [HttpPost]
        public IActionResult PayNow()
        {
            // Load OrderHeader with both navigation properties
            OrderVM.OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id, includeProperties: "applicationUser");
            OrderVM.OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.Id == OrderVM.OrderHeader.Id, includeProperties: "Product").ToList();

            // Make sure OrderDetail list is populated


            // Stripe logic
            var domain = "http://localhost:5067/";

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                SuccessUrl = domain + $"Admin/Order/PaymentConfirmation/{OrderVM.OrderHeader.Id}",
                CancelUrl = domain + $"Admin/Order/Details?orderId={OrderVM.OrderHeader.Id}",
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                Mode = "payment",
            };

            foreach (var item in OrderVM.OrderDetail)
            {
                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.price * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title
                        }
                    },
                    Quantity = item.Count
                };
                options.LineItems.Add(sessionLineItem);
            }

            var service = new Stripe.Checkout.SessionService();
            Stripe.Checkout.Session session = service.Create(options);

            _unitOfWork.OrderHeader.UpdateStripePaymentId(
                OrderVM.OrderHeader.Id, session.Id, session.PaymentIntentId
            );
            _unitOfWork.Save();

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }


        [HttpGet("PaymentConfirmation/{orderHeaderId}")]

        public IActionResult PaymentConfirmation(int orderHeaderId)
        {

            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(x => x.Id == orderHeaderId);
            if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)


            {
                // this is a Company payment
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeaderId, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(orderHeaderId, orderHeader.OrderStatus, SD.PaymentStatusApproved);
                    _unitOfWork.Save();

                }





            }
            return View(orderHeaderId);
        }



        #region API CALLS

        // GetAll API for DataTables
        [HttpGet("GetAll")]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> ObjOrderHeaders;

            if (User.IsInRole(SD.AdminRole) || User.IsInRole(SD.EmployeeRole))
            {
                ObjOrderHeaders = _unitOfWork.OrderHeader
                .GetAll(includeProperties: "applicationUser")
                .ToList();
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                ObjOrderHeaders = _unitOfWork.OrderHeader.GetAll(u => u.ApplicationUserId == userId, includeProperties: "applicationUser");

            }

            // Filter based on status
            if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
            {
                switch (status.ToLower())
                {
                    case "pending":
                        ObjOrderHeaders = ObjOrderHeaders
                            .Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                        break;
                    case "inprocess":
                        ObjOrderHeaders = ObjOrderHeaders
                            .Where(u => u.OrderStatus == SD.StatusInProcess);
                        break;
                    case "completed":
                        ObjOrderHeaders = ObjOrderHeaders
                            .Where(u => u.OrderStatus == SD.StatusShipped);
                        break;
                    case "approved":
                        ObjOrderHeaders = ObjOrderHeaders
                            .Where(u => u.OrderStatus == SD.StatusApproved);
                        break;
                }
            }

            return Json(new { data = ObjOrderHeaders });
        }

        #endregion
    }
}
