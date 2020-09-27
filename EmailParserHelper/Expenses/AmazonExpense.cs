using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailParserHelper.Expenses
{
    public class AmazonExpenseEntry : ExpenseOrder
    {
        public AmazonExpenseEntry(string plainTextEmail) : base(plainTextEmail)
        {
            var expenseEntryRegex = @"(.*\r\n.*)\r\n*\s*Sold by";
            var matches = Regex.Matches(EmailBody, expenseEntryRegex);
            var NominalOrderTotalMatches = Regex.Matches(EmailBody, @"Item Subtotal\:\s\$(\d*\.\d*)");

            var NominalOrderTotalMatchStrings = from Match item in NominalOrderTotalMatches
                                                select item.Groups[1].Value;
            foreach (var item in NominalOrderTotalMatchStrings)
            {
                double.TryParse(item, out var output);
                NominalOrderTotal += output;
            }


           //     NominalOrderTotal = MatchNumber(@"Item Subtotal\:\s\$(\d*\.\d*)", 1);
            ReceiverName = MatchRegex(@"Your order will be sent to:\r\n([^\r\n]*)", 1).Trim();
            var orderIDmatches = Regex.Matches(EmailBody, @"Order #([\d\-]*)");


            var matchStrings = from Match item in matches
                               select item.Groups[1].Value;
            var orderIDs = (from Match item in orderIDmatches
                           select item.Groups[1].Value).Distinct().ToList();
            int i = 0;
            foreach (var item in matchStrings)
            {
                var expenseEntry = new ExpenseEntry();
                if (i < orderIDs.Count)
                {
                    expenseEntry.orderID = orderIDs[i];
                }
                else
                {
                    expenseEntry.orderID = orderIDs.Last() + "-" + i.ToString();
                }
                ++i;
                var hasQuantityRegex = Regex.Match(item.Trim(), @"^(\d+)\sx\s(.*)");
                if (hasQuantityRegex.Success)
                {
                    int quantity;
                    int.TryParse(hasQuantityRegex.Groups[1].Value, out quantity);
                    expenseEntry.Quantity = quantity;
                    expenseEntry.Name = hasQuantityRegex.Groups[2].Value.Trim();
                }
                else
                {
                    expenseEntry.Name = common.MatchRegex(item, @"(.*)\r\n\s*\$", 1).Trim();
                }

                double cost;
                double.TryParse(common.MatchRegex(item, @"\$(\d*\.\d*)", 1), out cost);
                expenseEntry.CostPerItem = cost;
                expenseEntries.Add(expenseEntry);
            }
        }

        protected string MatchRegex(string pattern, int group = 0)
        {
            return common.MatchRegex(EmailBody, pattern, group);
        }

        protected double MatchNumber(string pattern, int group = 0)
        {
            double output;
            var result = MatchRegex(pattern, group);
            double.TryParse(result, out output);
            return output;
        }
    }
}
