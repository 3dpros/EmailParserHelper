using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        }


        private void InitializeEtsyEmailTransactions(List<string> transactionEmailStrings)
        {



            foreach (string orderText in transactionEmailStrings)
            {
                var lines = orderText.Split('\n');
                var entries = new Dictionary<string, string>();
                foreach(var line in lines)
                {
                    var pair = line.Split(':');
                    if (pair.Length >= 2)
                    {
                        var key = pair[0].ToLower().Trim();
                        if (!entries.ContainsKey(key))
                        {
                            entries.Add(pair[0].ToLower().Trim(), pair[1].ToLower().Trim());
                        }
                    }
                }

                var transaction = new Transaction()
                {
                    ItemName = entries["item"],
                    rawText = orderText,
                    Quantity = int.Parse(new string(entries["quantity"].TakeWhile(char.IsDigit).ToArray())),

                };

                if (entries.ContainsKey("item price"))
                {
                    transaction.ItemPrice = double.Parse(entries["item price"].Remove(0,1));
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
                    }
                }
                if (entries.ContainsKey("set quantity"))
                {
                    transaction.Quantity *= int.Parse(new string(entries["set quantity"].TakeWhile(char.IsDigit).ToArray()));
                }
                if (entries.ContainsKey("options"))
                {
                    if (entries["options"].Contains("custom"))
                    {
                        transaction.Custom = true;
                    }
                }
                Transactions.Add(transaction);
            }
        }
    }


}
