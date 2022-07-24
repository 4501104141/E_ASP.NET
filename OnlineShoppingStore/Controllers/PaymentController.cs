using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PayPal.Api;
using OnlineShoppingStore.Others;

namespace OnlineShoppingStore.Controllers
{
    public class PaymentController : Controller
    {
        // GET: Payment
        public ActionResult PaymentWithPapal()
        {

            APIContext apicontext = PaypalConfiguration.GetAPIContext();
            try
            {

                string PayerId = Request.Params["PayerID"];
                if (string.IsNullOrEmpty(PayerId) && PayerId != null)
                {
                    string baseURi = Request.Url.Scheme + "://" + Request.Url.Authority +
                                     "PaymentWithPapal/PaymentWithPapal?";

                    var Guid = Convert.ToString((new Random()).Next(100000000));
                    var createPayment = this.CreatePayment(apicontext, baseURi + "guid=" + Guid);

                    var links = createPayment.links.GetEnumerator();
                    string paypalRedirectURL = null;

                    while (links.MoveNext())
                    {
                        Links lnk = links.Current;

                        if (lnk.rel.ToLower().Trim().Equals("approval_url"))
                        {
                            paypalRedirectURL = lnk.href;
                        }


                    }
                }
                else
                {
                    var guid = Request.Params["guid"];
                    var executedPaymnt = ExecutePayment(apicontext, PayerId, Session[guid] as string);


                    if (executedPaymnt.ToString().ToLower() != "approved")
                    {
                        return View("FailureView");
                    }

                }
            }
            catch (Exception)
            {
                return View("FailureView");


                //throw;
            }
            return View("SuccessView");

        }



        public ActionResult PaymentWithVnPay()
        {

            string url = ConfigurationManager.AppSettings["Url"];
            string returnUrl = ConfigurationManager.AppSettings["ReturnUrl"];
            string tmnCode = ConfigurationManager.AppSettings["TmnCode"];
            string hashSecret = ConfigurationManager.AppSettings["HashSecret"];

            PayLib pay = new PayLib();


            string total = (int.Parse(Session["SesTotal"].ToString()) * 100).ToString();

            pay.AddRequestData("vnp_Version", "2.0.0"); //Phiên bản api mà merchant kết nối. Phiên bản hiện tại là 2.0.0
            pay.AddRequestData("vnp_Command", "pay"); //Mã API sử dụng, mã cho giao dịch thanh toán là 'pay'
            pay.AddRequestData("vnp_TmnCode", tmnCode); //Mã website của merchant trên hệ thống của VNPAY (khi đăng ký tài khoản sẽ có trong mail VNPAY gửi về)
            pay.AddRequestData("vnp_Amount", total); //số tiền cần thanh toán, công thức: số tiền * 100 - ví dụ 10.000 (mười nghìn đồng) --> 1000000
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

        public ActionResult PaymentConfirm()
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

                bool checkSignature = pay.ValidateSignature(vnp_SecureHash, hashSecret); //check chữ ký đúng hay không?

                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00")
                    {
                        //Thanh toán thành công
                        ViewBag.Message = "Thanh toán thành công hóa đơn " + orderId + " | Mã giao dịch: " + vnpayTranId;
                    }
                    else
                    {
                        //Thanh toán không thành công. Mã lỗi: vnp_ResponseCode
                        ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý hóa đơn " + orderId + " | Mã giao dịch: " + vnpayTranId + " | Mã lỗi: " + vnp_ResponseCode;
                    }
                }
                else
                {
                    ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý";
                }
            }

            return View();
        }

        private object ExecutePayment(APIContext apicontext, string payerId, string PaymentId)
        {
            var paymentExecution = new PaymentExecution() { payer_id = payerId };
            this.payment = new Payment() { id = PaymentId };
            return this.payment.Execute(apicontext, paymentExecution);
        }

        private PayPal.Api.Payment payment;

        private Payment CreatePayment(APIContext apicontext, string redirectURl)
        {

            var ItemLIst = new ItemList() { items = new List<Item>() };

            if (Session["cart"] != "")
            {
                List<Models.Home.Item> cart = (List<Models.Home.Item>)(Session["cart"]);
                foreach (var item in cart)
                {
                    ItemLIst.items.Add(new Item()
                    {
                        name = item.Product.ProductName.ToString(),
                        currency = "TK",
                        price = item.Product.Price.ToString(),
                        quantity = item.Product.Quantity.ToString(),
                        sku = "sku"
                    });


                }


                var payer = new Payer() { payment_method = "paypal" };

                var redirUrl = new RedirectUrls()
                {
                    cancel_url = redirectURl + "&Cancel=true",
                    return_url = redirectURl
                };

                var details = new Details()
                {
                    tax = "1",
                    shipping = "1",
                    subtotal = "1"
                };

                var amount = new Amount()
                {
                    currency = "USD",

                    total = Session["SesTotal"].ToString(),
                    details = details
                };

                var transactionList = new List<Transaction>();
                transactionList.Add(new Transaction()
                {
                    description = "Transaction Description",
                    invoice_number = "#100000",
                    amount = amount,
                    item_list = ItemLIst

                });

                this.payment = new Payment()
                {
                    intent = "sale",
                    payer = payer,
                    transactions = transactionList,
                    redirect_urls = redirUrl
                };
            }



            return this.payment.Create(apicontext);

        }
    }
}