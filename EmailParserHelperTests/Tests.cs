using System;
using Xunit;
using EmailParserHelper;
using System.Collections.Generic;

namespace EmailParserHelperTests
{
    public class UnitTest1
    {
        [Fact]
        public void parseEtsyEmail()
        {
            var email = @"------------------------------------------------------
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
Item:               Weight Plate Wall Art | Customizable 3D printed gym sign or wall sign for crossfit box
Color: Black
Size/Options: 11&quot; + Custom Text
Quantity:           1
Item price:         $48.00

Transaction ID:     1759746348
Item:               Weight Plate Clock | Customizable 3D printed gift for workout room, gym clock for crossfit
Size: 15&quot;
Options: Custom Text
Quantity:           1
Item price:         $72.00

Transaction ID:     1790457874
Item:               Weight Plate Wall Art | Customizable 3D printed gym sign or wall sign for crossfit box
Color: Gray
Size/Options: 18&quot; + Custom Text
Quantity:           1
Item price:         $90.00


Transaction ID:     1790761280
Item:               Dual Color Bumper Weight Ornament | 3d printed Crossfit ornament Christmas decoration lifter, bodybuilder, fitness gift
Color: Blue with White Text
Set Quantity: 3
Personalization:  Coach Whirry
Quantity:           1
Item price:         $48.00

--------------------------------------
Item total:         $90.00

--------------------------------------
Item total:         $125.00


Applied discounts
- CLITORISEARRINGSALE

Discount:          -$3.90
--------------------------------------
Subtotal:           $121.10

Shipping:           $0.00  ()
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

            * Email phillip.adler6 @gmail.com

             ------------------------------------------------------

If you have questions or were not involved in this transaction,
please contact our support team: http://www.etsy.com/help/contact

            Thanks,
Etsy

";
            var test = new EtsyOrder(email);
            var mystring = test.ShortDescription;
        }

        [Fact]
        public void ParseShopifyEmail()
        {
                        var email = @"
*  1x Full Size Model Clitoris (Gold) - Gold (SKU:ClitorisModel_gold) - $14.00 each ]]
*  2x Dumbbell - Gold | test () - $22.00 each ]]
*  3x Weight Plate - Gold () - $34.00 each ]]
_____
Order Note:

Shipping Cost: $0.00

Total Payment: $14.00

Customer Name: Miguel Rodriguez

Customer Email: 8djhv99rxh409qr@marketplace.asmazon.com

Order URL:
https://liftergifts.myshopify.com/admin/orders/1853957537907

Order ID: 1853957537907

Order Name: #1130

Payment processing method:

amazon_marketplace

Delivery method:
FreeEconomy

Shipping address:

Miguel Rodriguez

501 SW 98TH PL

MIAMI, Florida  33174-1961

United States

+1 412-532-4665 ext. 47569";
            var test = new ShopifyOrder(email);
            var mystring = test.ShortDescription;
            string orderID = "10";
            if(int.Parse(orderID) < 1000)
                {
                orderID = "1" + orderID;
            }

        }

    }
}
