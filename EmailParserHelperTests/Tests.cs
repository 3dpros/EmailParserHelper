using System;
using Xunit;
using EmailParserHelper;
using AirtableClientWrapper;
using System.Collections.Generic;
using System.Collections.Specialized;
using EmailParserHelper.Expenses;

namespace EmailParserHelperTests
{
    public class GeneralTests
    {
        [Fact]
        public void parseEtsyEmail()
        {
            var sut = new EtsyOrder(etsyEmailDesignCode, "");
            var test = sut.Transactions[0].HumanReadablePersonalization;
            Assert.Equal("1518764068", sut.OrderID);
            Assert.Equal("http://www.etsy.com/your/orders/1518764068", sut.OrderUrl);
            Assert.Equal(9, sut.Transactions.Count);

            var mystring = sut.LongDescription;
        }

        string htmlEmailSectionDays = @"    
< div class=""normal-copy copy""
     style=""font-family: arial, helvetica, sans-serif; color: #444444; font-size: 16px; line-height: 24px;"">    Processing time:
                                            3&ndash;122 business days</div>    
        </td>
    </tr>
</table></div>";


        string htmlEmailSectionWeeks = @"<div class=""normal-copy copy"" 
     style=""font-family: arial, helvetica, sans-serif; color: #444444; font-size: 16px; line-height: 24px;"">    Processing time:
                                            2&ndash;10 weeks</div>    
        </td>";

        [Fact]
        public void parseEtsyEmailWithHTML()
        {
            var sut = new EtsyOrder(etsyEmailDesignCode, htmlEmailSectionDays);
            Assert.Equal(122, sut.ProcessingTimeInDays);
            sut = new EtsyOrder(etsyEmailDesignCode, htmlEmailSectionWeeks);
            Assert.Equal(50, sut.ProcessingTimeInDays);
        }
        [Fact]
        public void ParseShopifyEmail()
        {

            var sut = new ShopifyOrder(shopifyEmail);
            Assert.Equal("1130", sut.OrderID);
            Assert.Equal(114.05, sut.OrderTotal);
            Assert.Equal("https://liftergifts.myshopify.com/admin/orders/1853957537907", sut.OrderUrl);
            Assert.Equal(2.04, sut.ShippingCharge);
            Assert.Equal(3, sut.Transactions.Count);

        }

        [Fact]
        public void ShipOrder()
        {
            var test = new Automation();
            test.CompleteOrder("1772497744", "0.00");

        }
        [Fact]
        public void ShipOrderParser()
        {
            var log = new List<string>();
            NameValueCollection fields = new NameValueCollection();
            fields.Add("Order ID", "1878");
            fields.Add("Shipping Cost", "0.00");

            var returnVal = ParserMethods.ProcessShippedProductOrder(ref log, fields, false);

        }

        [Fact]
        public void CreateInventoryRequestOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation();
            var component = inventoryBase.GetComponentByName("ZZZ - Dummy", false);
            var previousQuantity = component.Quantity;
            var previousPending = component.Pending;
            test.GenerateInventoryRequest(component, 3);
            Assert.Equal(component.Quantity, previousQuantity);
            Assert.True(component.Pending - previousPending == 3);
            previousPending = component.Pending;

                    test.GenerateInventoryRequest(component);
            component = inventoryBase.GetComponentByName("ZZZ - Dummy", false);
            Assert.Equal(component.Quantity, previousQuantity);
            Assert.True(component.Pending - previousPending == component.NumberOfBatches * component.BatchSize);
        }

        string shopifyEmail = @"**  3x Weight Plate Ornament - Default Text / Red (SKU:
WeightPlateOrnament_red) - $14.00 each ]]
*  3x Weight Plate Ornament - Default Text / Black with Gray Text
(SKU: WeightPlateOrnament_blacksilver) - $17.00 each ]]
*  3x Weight Plate Ornament - Default Text / Black (SKU:
WeightPlateOrnament_black) - $14.00 each ]]
*  3x Weight Plate Ornament - Default Text / Silver (SKU:
WeightPlateOrnament_silver) - $14.00 each ]]

_____

Shipping Cost: $25.00

Total Payment: $143.59

Customer Name: Kara Orser

Customer Email: karaonbroadway@hotmail.com

Order URL:
https://liftergifts.myshopify.com/admin/orders/3057119330483

Order ID: 3057119330483

Order Name: #1909

Payment processing method:

shopify_payments

Delivery method:
International Shipping

Shipping address:

Kara Orser

53 The Links Rd, Suite 200

Toronto, Ontario  M2P1T7

Canada";


        string etsyEmail = @"------------------------------------------------------
Your Etsy Order
------------------------------------------------------

Hi Al Billington,

We've finished processing your Etsy sale of 2 items.

Your order number is 1518764068.

View the invoice: 
http://www.etsy.com/your/orders/1518764068

------------------------------------------------------
Note from phillip.adler6@gmail.com:
------------------------------------------------------

The buyer did leave a notae.
another line too.


------------------------------------------------------
Order Details
------------------------------------------------------

Shop:               Al Billington

--------------------------------------

Transaction ID:     1738164749
Item:               Clitoris Ornament 3-Pack | 3D printed Full Size Anatomical Model of Clitoris
Set Quantity: 3
OtherOption: 32
Quantity:           1
Item price:         $65.00

Transaction ID:     1733107662
Item:               3D Printed Clitoris Earrings
Color: Purple
Quantity:           1
Item price:         $26.00

Transaction ID:     1733107660
Item:               3D Printed Clitoris Model | Full Size Anatomical Model of Clitoris
Color: Pink
Size: 10 in
Quantity: 3
Quantity:           4
Item price:         $34.00

Transaction ID:     1738603324
Item:               New Item | Customizable 3D printed gym sign or wall sign for crossfit box
Color: Black
Size/Options: 11&quot; + Custom Text
Quantity:           1
Item price:         $48.00


Transaction ID:     1962639347
Item:               Weight Plate Clock | 3D printed  fitness gift for workout room, gym clock for crossfit
Size: 15&quot;
Quantity:           1
Item price:         $58.00

Transaction ID:     1790457874
Item:               Weight Plate Wall Art | Customizable 3D printed gym sign or wall sign for crossfit box
Color: Gray
Size/Options: 18&quot; + Custom Text
Personalization:  I : Like colons

Quantity:           1
Item price:         $90.00


Transaction ID:     1790761280
Item:               Dual Color Bumper Weight Ornament | 3d printed Crossfit ornament Christmas decoration lifter, bodybuilder, fitness gift
Color: Blue with White Text
Set Quantity: 3
Personalization:  This is line one of two
here is the other line
Quantity:           1
Item price:         $48.00


Transaction ID:     1872006280
Item:               Weight Plate Custom Text Clock | Customizable 3D printed gift for workout room, gym clock for crossfit
Size: 15&quot;
Personalization:  TopText=Maguire%E2%80%99s|BottomText=JIM&#39;S|color=gray
Quantity:           1
Item price:         $68.00

Transaction ID:     1921038848
Item:               3D Printed UT Tower Model | Austin Skyline Downtown Building
Height: 8 Inches
Base Option: No Base
Quantity:           1
Item price:         $28.00

--------------------------------------
Item total:         $90.00

--------------------------------------
Item total:         $125.00


Applied discounts
- CLITORISEARRINGSALE

Discount:          -$3.90
--------------------------------------
Subtotal:           $121.10

Shipping:           $2.00  ()
Sales Tax:          $8.79
--------------------------------------
Order Total:        $129.89

Shipping Address:
<address >

<span class='name'>phillip adler</span><br/><span class='first-line'>2725 se 32nd ave</span><br/><span class='city'>PORTLAND</span>, <span class='state'>OR</span> <span class='zip'>97202</span><br/><span class='country-name'>United States</span>
<br/>


<!-- Hidden Fields -->
<input type=""hidden"" name=""country_code"" value=""209""/>

< !--Address Verification-- >

</ address >

------------------------------------------------------
Contacting the Buyer
------------------------------------------------------

*Send a message with Etsy's messaging system:
http://www.etsy.com/conversations/new?with_id=247009438

            Or

            * Email phillip.adler6@gmail.com

             ------------------------------------------------------

If you have questions or were not involved in this transaction,
please contact our support team: http://www.etsy.com/help/contact

            Thanks,
Etsy

";
        string etsyEmailDesignCode = @"------------------------------------------------------
Your Etsy Order
------------------------------------------------------

Hi Al Billington,

We've finished processing your Etsy sale of one item.

Your order number is 1676111111.

View the invoice: 
http://www.etsy.com/your/orders/1676111111

------------------------------------------------------
Note from oye5rdfl:
------------------------------------------------------

Top text in all caps: TEST
Bottom text in all caps: ORDER
Color: black
Thank you!


------------------------------------------------------
Order Details
------------------------------------------------------

Shop:               Al Billington

--------------------------------------

Transaction ID:     1931393625
Item:               Weight Plate Custom Text Clock | Customizable 3D printed gift for workout room, gym clock for crossfit
Size: 11&quot;
Personalization:  TopText=TEST|BottomText=ORDER|color=black
Quantity:           1
Item price:         $56.00

--------------------------------------
Item total:         $56.00




Shipping:           $0.00  ()
Sales Tax:          $4.34
--------------------------------------
Order Total:        $60.34


Shipping Address:
<address >

<span class='name'>Carolina Tjhin</span><br/><span class='first-line'>111 Cradle Bar Court</span><br/><span class='city'>FOLSOM</span>, <span class='state'>CA</span> <span class='zip'>95630</span><br/><span class='country-name'>United States</span>
<br/>


<!-- Hidden Fields -->

<!-- Address Verification -->

</address>

------------------------------------------------------
Contacting the Buyer
------------------------------------------------------

* Send a message with Etsy's messaging system:
http://www.etsy.com/conversations/new?with_id=284844832

Or

* Email lularoecarolinahylton @gmail.com

------------------------------------------------------

If you have questions or were not involved in this transaction,
please contact our support team: http://www.etsy.com/help/contact

Thanks,
Etsy
";
        string etsyEmailDummy = @"------------------------------------------------------
Your Etsy Order
------------------------------------------------------

Hi Al Billington,

We've finished processing your Etsy sale of one item.

Your order number is 1772497744.

View the invoice: 
http://www.etsy.com/your/orders/1772497744

------------------------------------------------------
Note from samanthatomarchio@gmail.com:
------------------------------------------------------

I'm messaging you!!!

------------------------------------------------------
Order Details
------------------------------------------------------

Shop:               Al Billington

--------------------------------------

Transaction ID:     2124091829
Item:               zzz - dummy item
Quantity:           1
Item price:         $5.00

--------------------------------------
Item total:         $5.00

Applied discounts
- ICANWAIT


Shipping:           $0.00  ()
Tax:                $0.00
--------------------------------------
Order Total:        $6.00


Shipping Address:
<address >

<span class='name'>Samantha Tomarchio</span><br/><span class='first-line'>3390 Dwight Way</span><br/><span class='city'>BERKELEY</span>, <span class='state'>CA</span> <span class='zip'>94704</span><br/><span class='country-name'>United States</span>
<br/>
";


        [Fact]
        public void CreateEtsyOrderDesignCode()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation(true);
            Order order;
            test.ProcessOrder(etsyEmailDesignCode, "", "Etsy", out order, out _);

        }
        [Fact]

        public void CreateEtsyOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation();
            Order order;
            test.ProcessOrder(etsyEmailDummy, "", "Etsy", out order, out _);

        }

        [Fact]
        public void CreateShopifyOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation(true);
            Order order;
            test.ProcessOrder(shopifyEmail, "", "Shopify", out order, out _);
        }

        [Fact]
        public void CompleteInventoryRequestOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation();
            var component = inventoryBase.GetComponentByName("ZZZ - Dummy", false);
            var previousQuantity = component.Quantity;
            var previousPending = component.Pending;
            test.CompleteInventoryRequest("ZZZ - Dummy", 3, 5);
            inventoryBase.UpdateComponentRecord(component);

            component = inventoryBase.GetComponentByName("ZZZ - Dummy", false);
            Assert.True(component.Quantity - previousQuantity == 3);
            Assert.True(component.Pending - previousPending == -5);
        }

        string amazonExpenseEmail = @"Amazon.com Order Confirmation
Order #112-2455119-0606629
www.amazon.com/ref=TE_tex_h
_______________________________________________________________________________________

Hello 3DPros LLC,

Thank you for shopping with us. We’ll send a confirmation once your items have shipped.

Your order details are indicated below. The payment details of your transaction can be found at:
https://www.amazon.com/gp/css/summary/print.html/ref=TE_oi?ie=UTF8&orderID=112-2455119-0606629

If you would like to view the status of your order or make any changes to it, please visit Your Orders on Amazon.com at:
https://www.amazon.com/gp/css/your-orders-access/ref=TE_gs


This order is placed on behalf of 3DPros.
     Your guaranteed delivery date is:
               tomorrow, October 24

                
     Your shipping speed:
               One-Day Shipping


     Your order will be sent to:
               Al (3DPros)
               PFLUGERVILLE, TX
               United States
=======================================================================================

Order Details
Order #113-5889773-0774657
Placed on today, December 8

               Inkbird ITC-1000F 2 Stage Temperature Controller Cooling and Heating Modes Celsius and Fahrenheit
               $15.99

               Sold by: Inkbird

               Condition: New

               KKBESTPACK 4 x 8 Inch Kraft Bubble Mailers Padded Shipping Envelopes 50 Pcs
               $7.95

               Sold by: Amazon.com Services LLC

_______________________________________________________________________________________


              Order Total: $29.93


=======================================================================================


To learn more about ordering, go to Ordering from Amazon.com at:
www.amazon.com/gp/help/customer/display.html/ref=TE_tex_ofa?nodeId=468466

If you want more information or need more assistance, go to Help at:
www.amazon.com/gp/help/customer/display.html/ref=TE_tex_ss?ie=UTF8&nodeId=508510

Thank you for shopping with us.
Amazon.com
www.amazon.com/ref=TE_tex_ty
_______________________________________________________________________________________

The payment for your invoice is processed by Amazon Payments, Inc. P.O. Box 81226 Seattle, Washington 98108-1226. If you need more information, please contact (866) 216-1075

Unless otherwise noted, items sold by Amazon.com are subject to sales tax in select states in accordance with the applicable laws of that state. If your order contains one or more items from a seller other than Amazon.com, it may be subject to state and local sales tax, depending upon the seller's business policies and the location of their operations. Learn more about tax and seller information at:
https://www.amazon.com/gp/help/customer/display.html/ref=hp_bc_nav?ie=UTF8&nodeId=202029700

This email was sent from a notification-only address that cannot accept incoming email. Please do not reply to this message.

    

     ";
        [Fact]
        public void  AddAmazonExpense()
        {
            var expense = new AmazonExpenseEntry(amazonExpenseEmail);
            Assert.True(expense.getOveragesPaid() < .2);
        }


    }
    public class ParserTests
    {
        [Fact]
        public void CompleteInventoryRequestOrder_ParserMethod()
        {
            var log = new List<string>();
           // ParserMethods.CreateManualInventoryOrder("ZZZ - Dummy", 10);
            var fields = new NameValueCollection();
            fields.Add("Task Name", "(10/20) ZZZ - Dummy");
            ParserMethods.CompleteInventoryRequestOrder(fields, ref log, true);
        }

    }
}
