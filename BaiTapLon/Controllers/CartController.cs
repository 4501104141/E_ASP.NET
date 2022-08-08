using BaiTapLon.Models;
using Mood.Draw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Mood.EF2;
using System.Configuration;
using CommomSentMail;
using BaiTapLon.Common;
using BaiTapLon.MoMo_API;
using Newtonsoft.Json.Linq;
using System.Net;
using API_NganLuong;
using BaiTapLon.VN_PAY;

namespace BaiTapLon.Controllers
{
    public class CartController : Controller
    {
        // GET: Cart
        private const string CartSession = "CartSession";// hằng số không thể đổi
        private const string OrderIDDel = "OrderID";
        public ActionResult Index()
        {
            var cart = Session[CartSession];

            var list = new List<CartItem>();
            ViewBag.totalProduct = 0;
            ViewBag.title = "Giỏ hàng";
            if (cart != null)
            {
                list = (List<CartItem>)cart;
                foreach (var item in list)
                {
                    ViewBag.totalProduct += (item.Quantity * item.Product.GiaTien);
                }
            }
            return View(list);
        }
        //Nhận 2 giá trị proID và số lượng.
        [HttpGet]
        public JsonResult AddItem(long productID, int quantity)
        {

            var product = new SanphamDraw().getByID(productID);
            var cart = Session[CartSession];
            var sessionUser = (UserLogin)Session[Constant.USER_SESSION];
            if (cart != null)
            {
                var list = (List<CartItem>)cart;// nếu nó có rồi nó sẽ ép kiểu sang kiẻu list
                //Nếu chứa productID thì nó mới cộng 1
                if (list.Exists(x => x.Product.IDContent == productID))
                {
                    foreach (var item in list)
                    {
                        if (item.Product.IDContent == productID)
                        {
                            item.Quantity += quantity;
                            //tăng số lượng sản phẩm khi thêm tiếp sản phẩm cùng ID.
                        }
                    }
                    var cartCount1 = list.Count();

                    return Json(
                        new
                        {
                            cartCount = cartCount1
                        }
                        , JsonRequestBehavior.AllowGet);
                }

                else
                {
                    //Chưa có sản phẩm như z trong giỏ.
                    //Tạo mới đối tượng cart item
                    var item = new CartItem();
                    item.Product = product;
                    item.Quantity = quantity;
                    list.Add(item);
                    item.countCart = list.Count();
                    var cartCount1 = list.Count();
                    //Gán vào session

                    return Json(
                        new
                        {
                            cartCount = cartCount1
                        }
                        , JsonRequestBehavior.AllowGet);

                }

            }

            else
            {
                //Tạo mới đối tượng cart item
                var item = new CartItem();
                item.Product = product;
                item.Quantity = quantity;
                item.countCart = 1;
                var list = new List<CartItem>();

                list.Add(item);
                //Gán vào session
                Session[CartSession] = list;

            }

            return Json(
                 new
                 {
                     cartCount = 1
                 }
                , JsonRequestBehavior.AllowGet);


        }


        public JsonResult Update(string cartModel)
        {
            var jsonCart = new JavaScriptSerializer().Deserialize<List<CartItem>>(cartModel);
            var sessionCart = (List<CartItem>)Session[CartSession];

            foreach (var item in sessionCart)
            {
                var jsonItem = jsonCart.SingleOrDefault(x => x.Product.IDContent == item.Product.IDContent);
                {
                    //đúng sản phẩm ấy
                    if (jsonItem != null)
                    {
                        item.Quantity = jsonItem.Quantity;
                    }
                }

            }
            //sau khi cập nhật gán lại session lại
            Session[CartSession] = sessionCart;
            return Json(new
            {
                status = true
            });// trả về cho res bằng true, bản chất gọi đến sever để làm việc
        }
        public JsonResult DeleteAll()
        {
            Session[CartSession] = null;
            return Json(new
            {
                status = true
            });// trả về cho res bằng true, bản chất gọi đến sever để làm việc
        }

        public JsonResult Delete(long id)
        {
            //vẫn lấy ra Danh sách giỏ hàng
            var sessionCart = (List<CartItem>)Session[CartSession];

            sessionCart.RemoveAll(x => x.Product.IDContent == id);
            Session[CartSession] = sessionCart;
            return Json(new
            {
                status = true
            });// trả về cho res bằng true, bản chất gọi đến sever để làm việc
        }

        [HttpGet]
        public ActionResult PaymentMoMo()
        {
            var sessionUser = (UserLogin)Session[Constant.USER_SESSION];
            var list = new List<CartItem>();
            if (sessionUser != null)
            {
                var userLogin = new UserDraw().getByIDLogin(sessionUser.userId);
                ViewBag.LoginUser = userLogin;

            }

            ViewBag.totalProduct = 0;
            var cart = Session[CartSession];
            if (cart != null)
            {
                list = (List<CartItem>)cart;
                ViewBag.listCart = list;
            }
            return View(list);
        }

        [HttpPost]
        public ActionResult PaymentMoMo(string shipName, string shipAddress, string shipMobile, string shipMail)
        {
            string sumOrder = Request["sumOrder"];
            string payment_method = Request["payment_method"];
            Random rand = new Random((int)DateTime.Now.Ticks);
            int numIterations = 0;
            numIterations = rand.Next(1, 100000);
            DateTime time = DateTime.Now;
            string orderCode = XString.ToStringNospace(numIterations + "" + time);
            var cart = (List<CartItem>)Session[CartSession];
            ViewBag.listCart = cart;
            foreach (var itemQuantity in cart)
            {
                if (itemQuantity.Quantity <= itemQuantity.Product.Soluong)
                {
                    if (payment_method.Equals("COD"))
                    {

                        var sum = 0;
                        foreach (var item in cart)
                        {
                            var price_sale = 0;
                            if (item.Product.PriceSale != null)
                            {
                                price_sale = (int)item.Product.PriceSale;
                            }
                            var price_deal = (item.Product.GiaTien - item.Product.GiaTien / 100 * (price_sale));
                            sum += price_deal * item.Quantity;
                        }

                        var resultOrder = saveOrder(shipName, shipAddress, shipMobile, shipMail, payment_method, orderCode);
                        if (resultOrder)
                        {
                            var OrderInfo = new OrderDraw().getOrderByOrderCode(orderCode);//db.Orders.Where(m => m.Code == orderId).FirstOrDefault();
                            ViewBag.paymentStatus = OrderInfo.StatusPayment;
                            ViewBag.Methodpayment = OrderInfo.DeliveryPaymentMethod;
                            ViewBag.Sum = sum;
                            Session[CartSession] = null;
                            return View("oderComplete", OrderInfo);

                        }
                        else
                        {
                            return Redirect("/loi-thanh-toan");
                        }
                    }
                    else
                    {
                        if (payment_method.Equals("MOMO"))
                        {
                            Session[OrderIDDel] = null;
                            //request params need to request to MoMo system
                            string endpoint = momoInfo.endpoint;
                            string partnerCode = momoInfo.partnerCode;
                            string accessKey = momoInfo.accessKey;
                            string serectkey = momoInfo.serectkey;
                            string orderInfo = momoInfo.orderInfo;
                            string returnUrl = momoInfo.returnUrl;
                            string notifyurl = momoInfo.notifyurl;

                            string amount = sumOrder;
                            string orderid = Guid.NewGuid().ToString();
                            string requestId = Guid.NewGuid().ToString();
                            string extraData = "";

                            //Before sign HMAC SHA256 signature
                            string rawHash = "partnerCode=" +
                                partnerCode + "&accessKey=" +
                                accessKey + "&requestId=" +
                                requestId + "&amount=" +
                                amount + "&orderId=" +
                                orderid + "&orderInfo=" +
                                orderInfo + "&returnUrl=" +
                                returnUrl + "&notifyUrl=" +
                                notifyurl + "&extraData=" +
                                extraData;
                            MoMoSecurity crypto = new MoMoSecurity();
                            //sign signature SHA256
                            string signature = crypto.signSHA256(rawHash, serectkey);


                            //build body json request
                            JObject message = new JObject
                    {
                { "partnerCode", partnerCode },
                { "accessKey", accessKey },
                { "requestId", requestId },
                { "amount", amount },
                { "orderId", orderid },
                { "orderInfo", orderInfo },
                { "returnUrl", returnUrl },
                { "notifyUrl", notifyurl },
                { "extraData", extraData },
                { "requestType", "captureMoMoWallet" },
                { "signature", signature }

                 };
                            ServicePointManager.Expect100Continue = true;
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            string responseFromMomo = PayMentRequest.sendPaymentRequest(endpoint, message.ToString());
                            JObject jmessage = JObject.Parse(responseFromMomo);


                            var resultOrder = saveOrder(shipName, shipAddress, shipMobile, shipMail, payment_method, orderid);
                            Session[OrderIDDel] = orderCode;
                            if (resultOrder)
                            {
                                return Redirect(jmessage.GetValue("payUrl").ToString());
                            }
                            else
                            {
                                return Redirect("/loi-thanh-toan");
                            }
                        }

                        //Neu Thanh toan Ngan Luong
                        else if (payment_method.Equals("NL"))
                        {
                            Session[OrderIDDel] = null;
                            string str_bankcode = Request["bankcode"];
                            RequestInfo info = new RequestInfo();
                            info.Merchant_id = nganluongInfo.Merchant_id;
                            info.Merchant_password = nganluongInfo.Merchant_password;
                            info.Receiver_email = nganluongInfo.Receiver_email;
                            info.cur_code = "vnd";
                            info.bank_code = str_bankcode;
                            info.Order_code = orderCode;
                            info.Total_amount = sumOrder;
                            info.fee_shipping = "0";
                            info.Discount_amount = "0";
                            info.order_description = "Thanh toán ngân lượng cho đơn hàng";
                            info.return_url = nganluongInfo.return_url;
                            info.cancel_url = nganluongInfo.cancel_url;
                            info.Buyer_fullname = shipName;
                            info.Buyer_email = shipMail;
                            info.Buyer_mobile = shipMobile;
                            ServicePointManager.Expect100Continue = true;
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            APICheckoutV3 objNLChecout = new APICheckoutV3();
                            ResponseInfo result = objNLChecout.GetUrlCheckout(info, payment_method);
                            // neu khong gap loi gi
                            if (result.Error_code == "00")
                            {
                                saveOrder(shipName, shipAddress, shipMobile, shipMail, payment_method, orderCode);
                                Session[OrderIDDel] = orderCode;
                                // chuyen sang trang ngan luong
                                return Redirect(result.Checkout_url);
                            }
                            else
                            {

                                ViewBag.status = false;
                                return View("cancel_order");
                            }

                        }
                        //Neu Thanh Toán ATM online
                        else if (payment_method.Equals("ATM_ONLINE"))
                        {
                            Session[OrderIDDel] = null;
                            string str_bankcode = Request["bankcode"];
                            RequestInfo info = new RequestInfo();
                            info.Merchant_id = nganluongInfo.Merchant_id;
                            info.Merchant_password = nganluongInfo.Merchant_password;
                            info.Receiver_email = nganluongInfo.Receiver_email;
                            info.cur_code = "vnd";
                            info.bank_code = str_bankcode;
                            info.Order_code = orderCode;
                            info.Total_amount = sumOrder;
                            info.fee_shipping = "0";
                            info.Discount_amount = "0";
                            info.order_description = "Thanh toán ngân lượng cho đơn hàng";
                            info.return_url = nganluongInfo.return_url;
                            info.cancel_url = nganluongInfo.cancel_url;
                            info.Buyer_fullname = shipName;
                            info.Buyer_email = shipMail;
                            info.Buyer_mobile = shipMobile;
                            ServicePointManager.Expect100Continue = true;
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            APICheckoutV3 objNLChecout = new APICheckoutV3();
                            ResponseInfo result = objNLChecout.GetUrlCheckout(info, payment_method);
                            // neu khong gap loi gi
                            if (result.Error_code == "00")
                            {
                                var resultNL = saveOrder(shipName, shipAddress, shipMobile, shipMail, payment_method, orderCode);
                                Session[OrderIDDel] = orderCode;
                                return Redirect(result.Checkout_url);
                            }
                            else
                            {
                                return View("cancel_order");
                            }
                        }

                        //Neu Thanh Toán VNPAY
                        else if (payment_method.Equals("VNPAY"))
                        {
                            Session[OrderIDDel] = null;
                            string str_bankcode = Request["bankcode"];
                            RequestInfo info = new RequestInfo();
                            info.Merchant_id = nganluongInfo.Merchant_id;
                            info.Merchant_password = nganluongInfo.Merchant_password;
                            info.Receiver_email = nganluongInfo.Receiver_email;
                            info.cur_code = "vnd";
                            info.bank_code = str_bankcode;
                            info.Order_code = orderCode;
                            info.Total_amount = sumOrder;
                            info.fee_shipping = "0";
                            info.Discount_amount = "0";
                            info.order_description = "Thanh toán VNPAY cho đơn hàng";
                            info.return_url = nganluongInfo.return_url;
                            info.cancel_url = nganluongInfo.cancel_url;
                            info.Buyer_fullname = shipName;
                            info.Buyer_email = shipMail;
                            info.Buyer_mobile = shipMobile;


                            return PayWithVNPay(int.Parse(sumOrder), shipName, shipAddress, shipMobile, shipMail, payment_method, orderCode);
                            //ServicePointManager.Expect100Continue = true;
                            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            //APICheckoutV3 objNLChecout = new APICheckoutV3();
                            //ResponseInfo result = objNLChecout.GetUrlCheckout(info, payment_method);
                            //// neu khong gap loi gi
                            //if (result.Error_code == "00")
                            //{
                            //    var resultNL = saveOrder(shipName, shipAddress, shipMobile, shipMail, payment_method, orderCode);
                            //    Session[OrderIDDel] = orderCode;
                            //    return Redirect(result.Checkout_url);
                            //}
                            //else
                            //{
                            //    return View("cancel_order");
                            //}
                        }
                    }
                }
                else
                {
                    ViewBag.Error = "Số lượng đặt hàng vượt quá số lượng sản phẩm cửa hàng";
                    return View("PaymentMoMo");
                }
            }

            return View();
        }
        public ActionResult Success(Orders OrderInfo)
        {

            return View(OrderInfo);

        }
        //Khi thanh toán Ngan Luong XOng
        public ActionResult confirm_orderPaymentOnline()
        {

            String Token = Request["token"];
            RequestCheckOrder info = new RequestCheckOrder();
            info.Merchant_id = nganluongInfo.Merchant_id;
            info.Merchant_password = nganluongInfo.Merchant_password;
            info.Token = Token;
            APICheckoutV3 objNLChecout = new APICheckoutV3();
            ResponseCheckOrder result = objNLChecout.GetTransactionDetail(info);
            var cart = (List<CartItem>)Session[CartSession];
            ViewBag.listCart = cart;
            var sum = 0;
            foreach (var item in cart)
            {
                var price_sale = 0;
                if (item.Product.PriceSale != null)
                {
                    price_sale = (int)item.Product.PriceSale;
                }
                var price_deal = (item.Product.GiaTien - item.Product.GiaTien / 100 * (price_sale));
                sum += price_deal * item.Quantity;
            }
            if (result.errorCode == "00")
            {

                var OrderInfo = new OrderDraw().getOrderByOrderCode(result.order_code);//db.Orders.Where(m => m.Code == orderId).FirstOrDefault();
                var order_detail = new OrderDraw().getProductByOrder_Details(OrderInfo.IDOder);
                foreach (var item in order_detail)
                {
                    new SanphamDraw().UpdateTonKho(item.ProductID, (int)item.Quanlity);
                }
                OrderInfo.StatusPayment = 1;
                new OrderDraw().UpdateTrangThaiThanhToan(OrderInfo);
                ViewBag.paymentStatus = OrderInfo.StatusPayment;
                ViewBag.Methodpayment = OrderInfo.DeliveryPaymentMethod;
                ViewBag.Sum = sum;
                Session["CartSession"] = null;
                return View("oderComplete", OrderInfo);
            }
            else
            {

                ViewBag.status = false;


            }
            return View("confirm_orderPaymentOnline");
        }
        public ActionResult confirm_orderPaymentOnline_momo()
        {

            String errorCode = Request["errorCode"];
            String oderCode = Request["orderId"];
            var cart = (List<CartItem>)Session[CartSession];
            ViewBag.listCart = cart;
            var sum = 0;
            foreach (var item in cart)
            {
                var price_sale = 0;
                if (item.Product.PriceSale != null)
                {
                    price_sale = (int)item.Product.PriceSale;
                }
                var price_deal = (item.Product.GiaTien - item.Product.GiaTien / 100 * (price_sale));
                sum += price_deal * item.Quantity;
            }
            if (errorCode == "0")
            {

                var OrderInfo = new OrderDraw().getOrderByOrderCode(oderCode);//db.Orders.Where(m => m.Code == orderId).FirstOrDefault();
                var order_detail = new OrderDraw().getProductByOrder_Details(OrderInfo.IDOder);

                foreach (var item in order_detail)
                {
                    new SanphamDraw().UpdateTonKho(item.ProductID, (int)item.Quanlity);
                }
                OrderInfo.StatusPayment = 1;// thanh toán thành công
                new OrderDraw().UpdateTrangThaiThanhToan(OrderInfo);
                ViewBag.paymentStatus = OrderInfo.StatusPayment;
                ViewBag.Methodpayment = OrderInfo.DeliveryPaymentMethod;
                ViewBag.Sum = sum;
                Session["CartSession"] = null;
                return View("oderComplete", OrderInfo);
            }

            else
            {

                ViewBag.status = false;
                return View("cancel_order_momo");
            }


        }

        public bool saveOrder(string shipName, string shipAddress, string shipMobile, string shipMail, string payment_method, string oderCode)
        {

            var userSession = (UserLogin)Session[Common.Constant.USER_SESSION];
            var order = new Orders();
            order.NgayTao = DateTime.Now;
            order.ShipName = shipName;
            order.ShipAddress = shipAddress;
            order.ShipEmail = shipMail;
            if (userSession != null)
            {
                order.CustomerID = userSession.userId;

            }
            order.ShipMobile = shipMobile;
            order.Status = 0;
            order.NhanHang = 0;
            order.GiaoHang = 0;
            if (payment_method.Equals("MOMO"))
            {
                order.DeliveryPaymentMethod = "Cổng thanh toán momo";
                order.OrderCode = oderCode;
            }
            if (payment_method.Equals("COD"))
            {
                order.DeliveryPaymentMethod = "COD";
                order.OrderCode = oderCode;
            }
            if (payment_method.Equals("ATM_ONLINE"))
            {
                order.DeliveryPaymentMethod = "ATM";
                order.OrderCode = oderCode;
            }
            if (payment_method.Equals("NL"))
            {
                order.DeliveryPaymentMethod = "Ngân Lượng";
                order.OrderCode = oderCode;
            }
            if (payment_method.Equals("VNPAY"))
            {
                order.DeliveryPaymentMethod = "VN Pay";
                order.OrderCode = oderCode;
            }
            order.StatusPayment = 2;
            var total = 0;
            var result = false;
            try
            {
                var detailDraw = new Order_DetailDraw();
                var idOrder = new OrderDraw().Insert(order);
                var cartItemProduct = (List<CartItem>)Session[CartSession];
                foreach (var item in cartItemProduct)
                {
                    //Insert Oder_Details
                    var order_Detail = new Order_Detail();
                    order_Detail.ProductID = item.Product.IDContent;
                    order_Detail.OderID = idOrder;
                    order_Detail.Quanlity = item.Quantity;
                    total += (item.Product.GiaTien * item.Quantity);
                    int temp = 0;
                    if (item.Product.PriceSale != null)
                    {
                        temp = (((int)item.Product.GiaTien) - ((int)item.Product.GiaTien / 100 * (int)item.Product.PriceSale));
                    }
                    else
                    {
                        temp = (int)item.Product.GiaTien;
                    }
                    order_Detail.Price = temp;
                    int topHot = (item.Product.Tophot + 1);
                    int soLuongUpdate = (item.Product.Soluong - item.Quantity);
                    var resTop = new SanphamDraw().UpdateTopHot(item.Product.IDContent, topHot);
                    //Update soluong moi
                    var rs = new SanphamDraw().UpdateSoLuong(item.Product.IDContent, soLuongUpdate);
                    result = detailDraw.Insert(order_Detail);

                }
                /*if (result)
                {
                    string content = System.IO.File.ReadAllText(Server.MapPath("~/Content/Home/template/newOrder.html"));
                    content = content.Replace("{{CustomerName}}", shipName);
                    content = content.Replace("{{Phone}}", shipMobile);
                    content = content.Replace("{{Email}}", shipMail);
                    content = content.Replace("{{Address}}", shipAddress);
                    content = content.Replace("{{Total}}", total.ToString("N0"));

                    var toMailAdmin = ConfigurationManager.AppSettings["ToEmailAddress"].ToString();
                    new MailHelper().sentMail(shipMail, "Đơn hàng mới từ Phương Nam Book", content);
                    new MailHelper().sentMail(toMailAdmin, "Đơn hàng mới từ Phương Nam Book", content);
                    Session[CartSession] = null;
                   
                    result = false;
                    return true;
                }
                */

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public ActionResult cancel_order_momo()
        {
            if (Session[OrderIDDel] != null)
            {
                string orderCode = Session[OrderIDDel].ToString();
                var OrderInfo = new OrderDraw().getOrderByOrderCode(orderCode);//db.Orders.Where(m => m.Code == orderId).FirstOrDefault();                                                        //OrderInfo.StatusPayment = 0;//huy thanh toán
                new OrderDraw().Delete(OrderInfo.IDOder);
                Session[OrderIDDel] = null;
                ViewBag.status = false;
            }
            return View();
        }

        public ActionResult cancel_order()
        {
            if (Session[OrderIDDel] != null)
            {
                string orderCode = Session[OrderIDDel].ToString();
                var OrderInfo = new OrderDraw().getOrderByOrderCode(orderCode);//db.Orders.Where(m => m.Code == orderId).FirstOrDefault();                                                        //OrderInfo.StatusPayment = 0;//huy thanh toán
                new OrderDraw().Delete(OrderInfo.IDOder);
                Session[OrderIDDel] = null;
                ViewBag.status = false;
            }
            return View();
        }

        public ActionResult PayWithVNPay(int totalAmount, String shipName, String shipAddress, String shipMobile, String shipMail, String payment_method, String orderCode)
        {
            string url = ConfigurationManager.AppSettings["Url"];
            string returnUrl =
                ConfigurationManager.AppSettings["ReturnUrl"] +
                "?shipName=" + shipName +
                "&shipAddress=" + shipAddress +
                "&shipMail=" + shipMail +
                "&payment_method=" + payment_method +
                "&orderCode=" + orderCode +
                "&sum=" + totalAmount
                ;

            string tmnCode = ConfigurationManager.AppSettings["TmnCode"];
            string hashSecret = ConfigurationManager.AppSettings["HashSecret"];

            PayLib pay = new PayLib();

            pay.AddRequestData("vnp_Version", "2.0.0"); //Phiên bản api mà merchant kết nối. Phiên bản hiện tại là 2.0.0
            pay.AddRequestData("vnp_Command", "pay"); //Mã API sử dụng, mã cho giao dịch thanh toán là 'pay'
            pay.AddRequestData("vnp_TmnCode", tmnCode); //Mã website của merchant trên hệ thống của VNPAY (khi đăng ký tài khoản sẽ có trong mail VNPAY gửi về)
            pay.AddRequestData("vnp_Amount", (totalAmount * 100).ToString()); //số tiền cần thanh toán, công thức: số tiền * 100 - ví dụ 10.000 (mười nghìn đồng) --> 1000000
            pay.AddRequestData("vnp_BankCode", ""); //Mã Ngân hàng thanh toán (tham khảo: https://sandbox.vnpayment.vn/apis/danh-sach-ngan-hang/), có thể để trống, người dùng có thể chọn trên cổng thanh toán VNPAY
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")); //ngày thanh toán theo định dạng yyyyMMddHHmmss
            pay.AddRequestData("vnp_CurrCode", "VND"); //Đơn vị tiền tệ sử dụng thanh toán. Hiện tại chỉ hỗ trợ VND
            pay.AddRequestData("vnp_IpAddr", Util.GetIpAddress()); //Địa chỉ IP của khách hàng thực hiện giao dịch
            pay.AddRequestData("vnp_Locale", "vn"); //Ngôn ngữ giao diện hiển thị - Tiếng Việt (vn), Tiếng Anh (en)
            pay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang"); //Thông tin mô tả nội dung thanh toán
            pay.AddRequestData("vnp_OrderType", "other"); //topup: Nạp tiền điện thoại - billpayment: Thanh toán hóa đơn - fashion: Thời trang - other: Thanh toán trực tuyến
            pay.AddRequestData("vnp_ReturnUrl", returnUrl); //URL thông báo kết quả giao dịch khi Khách hàng kết thúc thanh toán
            pay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString()); //mã hóa đơn

            string paymentUrl = pay.CreateRequestUrl(url, hashSecret);

            return Redirect(paymentUrl);
        }

        public ActionResult VNPayResult()
        {
            if (Request.QueryString.Count > 0)
            {
                string hashSecret = ConfigurationManager.AppSettings["HashSecret"]; //Chuỗi bí mật
                var vnpayData = Request.QueryString;
                PayLib pay = new PayLib();

                //lấy toàn bộ dữ liệu được trả về
                foreach (string s in vnpayData)
                {
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        pay.AddResponseData(s, vnpayData[s]);
                    }
                }

                long orderId = Convert.ToInt64(pay.GetResponseData("vnp_TxnRef")); //mã hóa đơn
                long vnpayTranId = Convert.ToInt64(pay.GetResponseData("vnp_TransactionNo")); //mã giao dịch tại hệ thống VNPAY
                string vnp_ResponseCode = pay.GetResponseData("vnp_ResponseCode"); //response code: 00 - thành công, khác 00 - xem thêm https://sandbox.vnpayment.vn/apis/docs/bang-ma-loi/
                string vnp_SecureHash = Request.QueryString["vnp_SecureHash"]; //hash của dữ liệu trả về
                string shipName = Request.QueryString["shipName"];
                string shipAddress = Request.QueryString["shipAddress"];
                string shipMobile = Request.QueryString["shipMobile"];
                string shipMail = Request.QueryString["shipMail"];
                string payment_method = Request.QueryString["payment_method"];
                string orderCode = Request.QueryString["orderCode"];
                int sum = int.Parse(Request.QueryString["sum"]);

                bool checkSignature = pay.ValidateSignature(vnp_SecureHash, hashSecret); //check chữ ký đúng hay không?

                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00")
                    {
                        bool resultOrder = saveOrder(shipName, shipAddress, shipMobile, shipMail, payment_method, orderCode);
                        Session[OrderIDDel] = orderCode;
                    
                        if (resultOrder)
                        {
                            var list = new List<CartItem>();
                            var cart = Session[CartSession];
                            if (cart != null)
                            {
                                list = (List<CartItem>)cart;
                                ViewBag.listCart = list;
                            }
                            var OrderInfo = new OrderDraw().getOrderByOrderCode(orderCode);//db.Orders.Where(m => m.Code == orderId).FirstOrDefault();
                            ViewBag.paymentStatus = OrderInfo.StatusPayment;
                            ViewBag.Methodpayment = OrderInfo.DeliveryPaymentMethod;
                            ViewBag.Sum = sum;
                            Session[CartSession] = null;
                            return View("oderComplete", OrderInfo);

                        }
                        else
                        {
                            return Redirect("/loi-thanh-toan");
                        }
                    }
                    else
                    {
                        //Thanh toán không thành công. Mã lỗi: vnp_ResponseCode
                        ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý hóa đơn " + orderId + " | Mã giao dịch: " + vnpayTranId + " | Mã lỗi: " + vnp_ResponseCode;
                        return Redirect("/loi-thanh-toan");
                    }
                }
                else
                {
                    ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý";
                    return Redirect("/loi-thanh-toan");
                }

            }
            return Redirect("/loi-thanh-toan");

            //return View();

        }

    }
}

