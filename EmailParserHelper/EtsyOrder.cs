using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace EmailParserHelper
{
    public class EtsyOrder : Order
    {

        public EtsyOrder(string plainTextEtsyOrderEmail)
        {
            string orderRegex = "Transaction ID(.|\\r|\\n)*?price:.*";

            var matches = Regex.Matches(plainTextEtsyOrderEmail, orderRegex);

            var matchStrings = from Match item in matches
                               select item.Groups[0].Value;
            InitializeEtsyEmailTransactions(matchStrings.ToList());

            string orderNotePattern = "Note [^\n]*\n-*(.*?)-{10}";
            string noOrderNotesPattern = "The buyer did not leave a note.";
            var orderNote = Regex.Match(plainTextEtsyOrderEmail, orderNotePattern, RegexOptions.Singleline).Groups[1].Value;
            if (!orderNote.Contains(noOrderNotesPattern))
            {
                Notes = orderNote;
            }

            string OrderURLPattern = "http://www.etsy.com/your/orders/(\\d*)";
            OrderUrl = Regex.Match(plainTextEtsyOrderEmail, OrderURLPattern, RegexOptions.Singleline)?.Groups[0]?.Value;
            OrderID = Regex.Match(plainTextEtsyOrderEmail, OrderURLPattern, RegexOptions.Singleline)?.Groups[1]?.Value;



        }


        private void InitializeEtsyEmailTransactions(List<string> transactionEmailStrings)
        {



            foreach (string orderText in transactionEmailStrings)
            {
                var lines = orderText.Split('\n');
                var entries = new Dictionary<string, string>();
                var key = "";
                foreach (var line in lines)
                {
                    var multiLineSupportedKeys = new List<string>{ "personalization" };
                    char[] separator = { ':' };
                    var pair = line.Split(separator, 2);

                    if (pair.Length >= 2)
                    {
                        key = pair[0].ToLower().Trim();
                        if (!entries.ContainsKey(key))
                        {
                            entries.Add(pair[0].ToLower().Trim(), HttpUtility.HtmlDecode(pair[1].Trim()));
                        }
                    }
                    //for multiline fields, check if there is a colon, and if not add it as a new line instead of a new field
                    if(pair.Length == 1 && multiLineSupportedKeys.Contains(key) && pair[0].Trim() != string.Empty)
                    {
                        entries[key] += $" / {HttpUtility.HtmlDecode(pair[0])}";
                    }
                }

                var transaction = new Transaction(itemName: entries["item"])
                {
                    rawText = orderText,
                    Quantity = int.Parse(new string(entries["quantity"].TakeWhile(char.IsDigit).ToArray())),
                };
                entries.Remove("quantity");
                if(entries.ContainsKey("transaction id"))
                {
                    entries.Remove("transaction id");
                }
                if (entries.ContainsKey("item"))
                {
                    entries.Remove("item");
                }
                if (entries.ContainsKey("item price"))
                {
                    transaction.ItemPrice = double.Parse(entries["item price"].Remove(0, 1));
                    entries.Remove("item price");
                }
                if (entries.ContainsKey("personalization"))
                {
                    transaction.Personalization = entries["personalization"];
                    entries.Remove("personalization");

                }
                foreach (var colorKey in new string[] { "color", "colour" })
                {
                    if (entries.ContainsKey(colorKey))
                    {
                        transaction.Color = entries[colorKey];
                        if (entries[colorKey].Contains("custom"))
                        {
                            transaction.Custom = true;
                        }
                        entries.Remove(colorKey);
                    }
                }
                foreach (var sizeKey in new string[] { "size", "size/options", "height", "length" })
                {
                    if (entries.ContainsKey(sizeKey))
                    {
                        //for clocks which share the custom option with size.  if custom is in the value, track it as custom but still get the size for material tracking (clock kits)
                        if (entries[sizeKey].Contains("custom"))
                        {
                            transaction.Custom = true;
                        }
                        {
                            transaction.SizeInInches = int.Parse(new string(entries[sizeKey].TakeWhile(char.IsDigit).ToArray()));
                        }
                        entries.Remove(sizeKey);
                    }
                }
                if (entries.ContainsKey("set quantity"))
                {
                    var setQuantity = int.Parse(new string(entries["set quantity"].TakeWhile(char.IsDigit).ToArray()));
                    transaction.Quantity *= setQuantity;
                    entries.Remove("set quantity");
                    transaction.ItemPriceQuantity = setQuantity;

                }
                if (entries.ContainsKey("options"))
                {
                    if (entries["options"].Contains("custom"))
                    {
                        transaction.Custom = true;
                    }
                    entries.Remove("options");
                }
                if (entries.Count > 0)
                {
                    transaction.CustomFields = entries;
                }
                Transactions.Add(transaction);
            }
        }
    }


}
