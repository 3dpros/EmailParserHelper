using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailParserHelper
{
    public abstract class Order
    {
        public Order(string emailBody)
        {
            EmailBody = emailBody;
        }
        public string EmailBody { get; }
        public abstract string OrderUrl { get; }
        public string OrderUrlMarkdown {
            get
            {
                return $"[{OrderID}]({OrderUrl})";
            }
        }

        public string OrderID { get; set; }
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        public string OneLineDescription
        {
            get
            {
                return GetDescription("  ||  ");
            }
        }
        public string ShortDescription
        {
            get
            {
                var desc = GetDescription("\r\n");
                if (Transactions.Count > 1)
                {
                    desc = $"[{TotalNumberOfItems} Total Items]\r\n" + desc;
                }
                return desc;
            }
        }

        private string GetDescription(string delimiter)
        {
            var shortDescription = string.Empty;
            var transactionStrings = (from items in Transactions
                                      select items.GetDescription());
            var itemsString = string.Join(delimiter, transactionStrings);
            return itemsString;
        }

        public string LongDescription
        {
            get
            {
                var longDescription = string.Empty;
                var transactionStrings = from items in Transactions
                                          select items.GetDescription(true);
                var itemsString = string.Join("\r\n", transactionStrings);
                longDescription = itemsString;

                if (!string.IsNullOrEmpty(Notes))
                {
                    longDescription = $"Note: {Notes.Trim()} \r\n\r\n{longDescription}";
                }
                return longDescription;
            }
        }
        public string Notes { get; set; } = "";
        public string ImageURL { get; set; } = "";
        public double OrderTotal { get; set; } = 0;
        public double ShippingCharge { get; set; } = 0;

        public int TotalNumberOfItems { get
            {
                return (from items in Transactions
                               select items.Quantity).Sum();
            }
        }
        

        public double SalesTax { get; set; } = 0;
        public Customer Customer { get; set; } = new Customer();

        public DateTime DueDate
        {
            get
            {
                if (UseBusinessDaysForProcessingTime)
                {
                    return AddBusinessDays(DateTime.Now, ProcessingTimeInDays);
                }
                else
                {
                    return DateTime.Now.AddDays(ProcessingTimeInDays);
                }
            }
        }

        protected int ProcessingTimeInDays = 6;
        protected bool UseBusinessDaysForProcessingTime = true;

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

        public static DateTime AddBusinessDays(DateTime date, int days)
        {
            if (days < 0)
            {
                throw new ArgumentException("days cannot be negative", "days");
            }

            if (days == 0) return date;

            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                date = date.AddDays(2);
                days -= 1;
            }
            else if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
                days -= 1;
            }

            date = date.AddDays(days / 5 * 7);
            int extraDays = days % 5;

            if ((int)date.DayOfWeek + extraDays > 5)
            {
                extraDays += 2;
            }

            return date.AddDays(extraDays);

        }
    }

    public class Customer
    {
        public string Username { get; set; }
        public string Email { get; set; }

    }

}
/*
public class MyScriptBasedEmailParser // do not modify class name
{
    // Modify this method to implement your own text capturing
    public List<string> ExtractTextFrom(string input_text)
    {
        string result = "";
        // captured_values holds the captured text associated
        // to the field name.
        List<string> captured_values = new List<string>();

        // And of course any text related operations are also possible
        // using the input_text


        string itemPricePattern = @"Item price:\s*\$(\d+.\d+)";
        string itemNamePattern = @"Item:\s* (?:3D Printed)?([^|\n\-]*)\s*[-|\n]";
        string itemQuantityPattern = @"Quantity:\s{3,100}(\d+)";
        string itemDetailsPattern = @"Item:.*\n((.|\n)*?)\n(?=Quantity)";

        string orderNumberPattern = @"Your order number is (\d+)";
        string orderNotePattern = "Note [^\n]*\n-*(.*?)-{10}";
        string noOrderNotesPattern = "The buyer did not leave a note.";

        string etsyURLBase = "https://www.etsy.com/your/orders/sold?order_id=";

        MatchCollection itemPrices = Regex.Matches(input_text, itemPricePattern, RegexOptions.IgnoreCase);
        MatchCollection itemNames = Regex.Matches(input_text, itemNamePattern, RegexOptions.IgnoreCase);
        MatchCollection itemDetails = Regex.Matches(input_text, itemDetailsPattern, RegexOptions.IgnoreCase);
        MatchCollection itemQuantity = Regex.Matches(input_text, itemQuantityPattern, RegexOptions.IgnoreCase);

        Match orderNumber = Regex.Match(input_text, orderNumberPattern, RegexOptions.IgnoreCase);
        Match orderNote = Regex.Match(input_text, orderNotePattern, RegexOptions.Singleline);

        List<string> itemNamesList = new List<string>();

        List<string> itemEntries = new List<string>();
        int i = 0;
        foreach (Match m in itemNames)
        {

            string itemDetailsString = "";
            string quantityPrefix = "";
            try
            {
                itemDetailsString = "(" + getMatchingString(itemDetails[i]).Replace("\r\n", " , ") + ")";
            }
            catch
            { }
            try
            {
                quantityPrefix = string.Format("({0}x) ", getMatchingString(itemQuantity[i]));
                itemNamesList.Add(getMatchingString(m));
                itemEntries.Add(string.Format("{0}{1}{2}", quantityPrefix, getMatchingString(itemNames[i]), itemDetailsString));
                ++i;
                if (i > 10000)
                    break;
            }
            catch
            { }
        }

        string orderURL = string.Format("{0}{1}", etsyURLBase, getMatchingString(orderNumber));

        bool isNotePresent = (getMatchingString(orderNote).IndexOf(noOrderNotesPattern) == -1);
        string notesDisplay = isNotePresent ? string.Format("Customer Note: \n{0}\n__________", getMatchingString(orderNote)) : "";
        string notesShortDisplay = isNotePresent ? "[Note] " : "";


        //Short Description
        captured_values.Add(string.Format("{0}{1}",
                    notesShortDisplay,
            string.Join("  ||  ", itemEntries)));

        return (captured_values);

    }

    protected string getMatchingString(Match match)
    {
        return (WebUtility.HtmlDecode(match.Groups[1].ToString().Trim()));
    }

}
*/
