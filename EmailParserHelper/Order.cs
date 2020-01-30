using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailParserHelper
{
    public class Order
    {
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        public string ShortDescription
        {
            get
            {
                var shortDescription = string.Empty;
                var transactionStrings = (from items in Transactions
                                          select items.ToString());
                var itemsString = string.Join(" || ", transactionStrings);
                if (string.IsNullOrEmpty(Notes))
                {
                    shortDescription = itemsString;
                }
                else
                {
                    shortDescription = $"[Note] {itemsString}";
                }
                return shortDescription;
            }
        }

        public string LongDescription
        {
            get
            {
                var longDescription = string.Empty;
                var transactionStrings = from items in Transactions
                                          select items.ToString();
                var itemsString = string.Join("\r\n", transactionStrings);
                if (string.IsNullOrEmpty(Notes))
                {
                    longDescription = itemsString;
                }
                else
                {
                    longDescription = $"[Note] {itemsString}";
                }
                return longDescription;
            }
        }
        public string Notes { get; set; } = "";
        public DateTime DueDate
        {
            get
            {
                return AddBusinessDays(DateTime.Now, ProcessingTimeInDays);
            }
        }

        protected int ProcessingTimeInDays = 6;
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
    public class Transaction
    {
        public string rawText { get; set; }
        public string ItemName { get; set; }
        public string CleanedItemName {
            get
            {
                var itemNamePattern = @"\s*(?:3D Printed)?([^|\n]*)\s*[-|\n]?";
                return getMatchingString(Regex.Match(ItemName, itemNamePattern, RegexOptions.IgnoreCase));
            }
        }

        public int Quantity { get; set; }
        public string Color { get; set; }
        public int SizeInInches { get; set; }
        public double ItemPrice { get; set; }
        public string SKU { get; set; }

        public bool Custom { get; set; }

        public override string ToString()
        {

            var baseName = $"({Quantity}x) {CleanedItemName}";
            var Options = new List<string>();
            if(!string.IsNullOrEmpty(Color))
            {
                Options.Add("Color: " + Color);
            }
            if (SizeInInches != 0)
            {
                Options.Add("Size: " + SizeInInches.ToString() + " in");
            }
            if(Options.Count > 0)
            {
                baseName += " (" + string.Join(", ", Options.ToArray()) + ")";
            }
            return baseName;
        }

        protected string getMatchingString(Match match)
        {
            return (WebUtility.HtmlDecode(match.Groups[1].ToString().Trim()));
        }
    }

}

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

