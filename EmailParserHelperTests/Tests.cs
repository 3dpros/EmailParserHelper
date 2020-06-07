using System;
using Xunit;
using EmailParserHelper;
using AirtableClientWrapper;
using System.Collections.Generic;

namespace EmailParserHelperTests
{
    public class UnitTest1
    {
        [Fact]
        public void parseEtsyEmail()
        {
            var sut = new EtsyOrder(etsyEmail);

            Assert.Equal("1518764068", sut.OrderID);
            Assert.Equal("http://www.etsy.com/your/orders/1518764068", sut.OrderUrl);
            Assert.Equal(8, sut.Transactions.Count);

            var mystring = sut.LongDescription;
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
        public void CompleteOrder()
        {
            var test = new Automation(true);
            test.CompleteOrder("1663955060", "1.25");
        }


        [Fact]
        public void CreateInventoryRequestOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation(true);
            var component = inventoryBase.GetComponentByName("ZZZ - Dummy", false);
            var previousQuantity = component.Quantity;
            var previousPending = component.Pending;
            test.GenerateInventoryRequest(component, 3);
            Assert.Equal(component.Quantity, previousQuantity);
            Assert.True(component.Pending - previousPending == 3);
            previousPending = component.Pending;

            test.GenerateInventoryRequest(component);
            Assert.Equal(component.Quantity, previousQuantity);
            Assert.True(component.Pending - previousPending == component.NumberOfBatches * component.BatchSize);
        }

        string shopifyEmail = @"
*  1x Full Size Model Clitoris (Gold) - Gold (SKU:ClitorisModel_gold) - $14.00 each ]]
*  2x Dumbbell - Gold | test () - $22.00 each ]]
*  3x Weight Plate - Gold (SKU:WeightPlateClock_11in) - $34.00 each ]]
_____
Order Note:

Shipping Cost: $2.04

Total Payment: $114.05

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
Height: 6 Inches
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

        [Fact]
        public void CreateEtsyOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation(true);
            Order order;
            test.ProcessOrder(etsyEmail, "Etsy", out order);

        }

        [Fact]
        public void CreateShopifyOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation(true);
            Order order;
            test.ProcessOrder(shopifyEmail, "Shopify", out order);
        }

        [Fact]
        public void CompleteInventoryRequestOrder()
        {
            var inventoryBase = new AirtableItemLookup();

            var test = new Automation();
            var component = inventoryBase.GetComponentByName("ZZZ - Dummy", false);
            var previousQuantity = component.Quantity;
            var previousPending = component.Pending;
            test.CompleteInventoryRequest(component, 3, 5);
            inventoryBase.UpdateComponentRecord(component);
            Assert.True(component.Quantity - previousQuantity == 3);
            Assert.True(component.Pending - previousPending == -5);
        }

    }
}
