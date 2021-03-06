﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace EmailParserHelper
{
    public class EtsyOrder : Order
    {
        static int EtsyMinimumProcessingDays = 4;

        public EtsyOrder(string plainTextEtsyOrderEmail, string HTMLOrderEmail) : base(plainTextEtsyOrderEmail)
        {
            string orderRegex = "Transaction ID(.|\\r|\\n)*?price:.*";

            var matches = Regex.Matches(EmailBody, orderRegex);

            var matchStrings = from Match item in matches
                               select item.Groups[0].Value;
            InitializeEtsyEmailTransactions(matchStrings.ToList());

            string orderNotePattern = "Note [^\n]*\n-*(.*?)-{10}";
            string noOrderNotesPattern = "The buyer did not leave a note.";
            var orderNote = Regex.Match(EmailBody, orderNotePattern, RegexOptions.Singleline).Groups[1].Value;
            if (!orderNote.Contains(noOrderNotesPattern))
            {
                Notes = orderNote;
            }
            if(EmailBody.Contains("Marked as gift"))
            {
                MarkedAsGift = true;
            }

            OrderID = MatchRegex(OrderURLPattern, 1);
            SalesTax = MatchNumber(@"Tax\:\s *\$([^\n]*)", 1);
            Customer.Email = MatchRegex(@"Email\s*([^\n\r]*)?",1);
            Customer.Username = MatchRegex(@"Note from\s*([^\n\r]*)?",1);
            OrderTotal = MatchNumber(@"Order Total\:\s*\$\s*([^\n\r]*)?",1);
            ShippingCharge = MatchNumber(@"Shipping\s*\:\s*\$\s*([^\s\n\r\(\)]*)", 1);
            ImageURL = Regex.Match(HTMLOrderEmail, @"(https:\/\/i.etsystatic.com\/.*)\""")?.Groups[1]?.Value;
            if(Regex.Match(plainTextEtsyOrderEmail, "ICANWAIT", RegexOptions.Singleline).Success)
            {
                ProcessingTimeInDays = 15;
            }
            var processingTimeFromEmail = Regex.Match(HTMLOrderEmail, @"Processing time.*?(\d+)\s(weeks|business days)", RegexOptions.Singleline);
            if (processingTimeFromEmail.Success)
            {
                var multiplier = (processingTimeFromEmail.Groups[2].Value == "weeks") ? 5 : 1; // use 5 for weeks since it is in business days
                ProcessingTimeInDays = int.Parse(processingTimeFromEmail.Groups[1].Value) * multiplier;
            }
            else // if we cant figure out the processing time from etsy, try to do it from the product processing times in airtable
            {
                var transactionsProcessingTimeList = (from txn in Transactions
                                                      where txn.ProductData != null
                                                      select txn.ProductData.ProcessingTime).ToList();
                if (transactionsProcessingTimeList.Count > 0)
                {
                    ProcessingTimeInDays = Math.Max(transactionsProcessingTimeList.Max(), EtsyMinimumProcessingDays);
                }
            }
        }

        public EtsyOrder(NameValueCollection fields) : base(fields)
        {
            OrderUrl = "https://www.etsy.com/your/orders/sold/125458821940?order_id=" + OrderID;
        }


        private readonly string OrderURLPattern = "http://www.etsy.com/your/orders/(\\d*)";

        private string _OrderUrl;
        public override string OrderUrl
        {
            get
            {
                var retval = MatchRegex(OrderURLPattern);
                if (string.IsNullOrEmpty(retval))
                {
                    retval = _OrderUrl;
                }
                return retval;
            }
            set
            {
                _OrderUrl = value;
            }
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
                foreach (var sizeKey in new string[] { "size", "size/options", "height", "length", "width", "misura" })
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
                            if(transaction.SizeInInches == 0)
                            {
                                transaction.SizeInInches = int.Parse(Regex.Match(entries[sizeKey], @"\d*").Value);
                            }
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
                foreach (var optionsKey in new string[] { "options", "model" })
                if (entries.ContainsKey(optionsKey))
                {
                    if (entries[optionsKey].Contains("custom"))
                    {
                        transaction.Custom = true;
                    }
                    transaction.ItemName += " - " + entries[optionsKey];
                    entries.Remove(optionsKey);
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
