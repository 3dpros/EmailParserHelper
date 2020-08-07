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

            NominalOrderTotal = MatchNumber(@"Item Subtotal\:\s\$(\d*\.\d*)", 1);
            ReceiverName = MatchRegex(@"Your order will be sent to:\r\n([^\r\n]*)", 1).Trim();
            orderID = MatchRegex(@"Order #([\d\-]*)", 1);

            var matchStrings = from Match item in matches
                               select item.Groups[1].Value;
            foreach (var item in matchStrings)
            {
                var expenseEntry = new ExpenseEntry();

                var hasQuantityRegex = Regex.Match(item, @"(\d+)\sx\s(.*)");
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
