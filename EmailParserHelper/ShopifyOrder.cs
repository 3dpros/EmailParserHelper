using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailParserHelper
{
    public class ShopifyOrder : Order
    {
        static int AmazonProcessingDays = 4;
        static int ShopifyProcessingDays = 6;



        public ShopifyOrder(string plainTextOrderEmail) : base(plainTextOrderEmail)
        {
            string orderRegex = @"\*\s*(\d+x\s.*?)-\s*\$";
            var matches = Regex.Matches(plainTextOrderEmail, orderRegex, RegexOptions.Singleline);

            var matchStrings = from Match item in matches
                               select item.Groups[1].Value.Trim();
            InitializeShopifyOrderEmailTransactions(matchStrings.ToList());
            string emailAddressRegex = @"Customer Email\:\s*(.*)";
            string amazonOrderPattern = @"marketplace\.amazon\.com";
            var emailAddress = Regex.Match(plainTextOrderEmail, emailAddressRegex).Groups[1].Value;
            var isAmazon = Regex.Match(emailAddress, amazonOrderPattern).Success;

            ProcessingTimeInDays = isAmazon? AmazonProcessingDays : ShopifyProcessingDays;
            UseBusinessDaysForProcessingTime = !isAmazon;

            OrderID = MatchRegex(@"Order Name:\s*#\s*([\d]*)", 1);
            Customer.Email = MatchRegex(@"Customer Email\:\s*([^\n\r]*)?", 1);
            OrderTotal = MatchNumber(@"Total Payment\:\s*\$\s*([^\n\r]*)?", 1);
            ShippingCharge = MatchNumber(@"Shipping Cost\s*\:\s*\$\s*([^\s\n\r\(\)]*)", 1);
        }

        public override string OrderUrl
        {
            get
            {
                return MatchRegex(@"Order URL\:\r\n([^\n\r]*)?", 1);
            }
        }

        //1x Weight Plate Ornament (SKU: WeightPlateOrnament_silver) - 
        //$9.00 each

        private void InitializeShopifyOrderEmailTransactions(List<string> transactionEmailStrings)
        {
            foreach (string orderText in transactionEmailStrings)
            {
                var itemName = Regex.Match(orderText + "\n", @"\d+x\s(.*?)[-(\r\n]").Groups[1]?.Value;
                var quantity = Regex.Match(orderText, @"\d+").Value;
                var sku = Regex.Match(orderText, @"\(SKU:\s*([^)]*)")?.Groups[1]?.Value;
                var setQuantityString = Regex.Match(orderText, @"\[(\d+)\]")?.Groups[1]?.Value;
                var setQuantity = 1;

                if (!string.IsNullOrEmpty(setQuantityString))
                {
                     setQuantity = int.Parse(setQuantityString);
                }

                var transaction = new Transaction(sku: sku)
                {
                    ItemName = itemName,
                    Quantity = int.Parse(quantity) * setQuantity,
                };

                Transactions.Add(transaction);
            }
        }
    }
}
