using AirtableClientWrapper;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace EmailParserHelper
{
    public class Transaction
    {
        public string rawText { get; set; }
        public string ItemName { get; set; }
        public string Personalization { get; set; }
        public Dictionary<string, string> CustomFields = new Dictionary<string, string>();
        public string PersonalizationWithSize {
            get
            {
                if (SizeInInches != 0 && !Personalization.Contains(" ") && Personalization.Contains("|"))
                {
                    return Personalization + "|sizeOpt=" + SizeInInches.ToString();
                }
                else
                {
                    return Personalization;
                }
            }
        }

        public string CleanedPersonalization {
            get
            {
                return HttpUtility.UrlDecode(Personalization);
            }
        }

        /* need to move this into component once this is needed */
        public string DesignCode
        {
            get
            {
                //make sure this syncs with the designer js code
                return PersonalizationWithSize.Replace("|", "&");
            }
        }
        public string BaseDesignerUrl
        {
            get
            {
                return ProductData.BaseUrl;
            }
        }

        public string DesignerUrlFull
        {
            get
            {
                if(!string.IsNullOrEmpty(BaseDesignerUrl) && !string.IsNullOrEmpty(Personalization))
                {
                    //dirty check for if it is a designer ID or something they typed
                    if (Personalization.Contains("=") && !DesignCode.Contains(" "))
                    {
                        return BaseDesignerUrl + "#" + DesignCode;
                    }
                }
                return string.Empty;
            }
        }
        
        public string CleanedItemName {
            get
            {
                if (string.IsNullOrEmpty(ProductData?.DisplayName))
                {
                    var itemNamePattern = @"\s*(?:3D Printed)?([^|\n]*)\s*[-|\n]?";
                    return getMatchingString(Regex.Match(ItemName, itemNamePattern, RegexOptions.IgnoreCase));
                }
                return ProductData?.DisplayName;
            }
        }
        private AirtableItemLookup inventoryBase = new AirtableItemLookup();

        public Transaction(string sku)
        {
            SKU = sku;
            ProductData = inventoryBase.FindItemRecordBySKU(SKU);
            if (ProductData != null)
            {
                ItemName = ProductData.ItemName;
                Color = ProductData.Color;
                SizeInInches = ProductData.Size;
            }
        }

        public Transaction(string itemName, string itemColor = "", int itemSizeInInches = 0)
        {
            ItemName = itemName;
            Color = itemColor;
            SizeInInches = itemSizeInInches;
            ProductData = inventoryBase.FindItemRecord(ItemName, Color, SizeInInches);
            IsDigital = itemName.Contains("STL File");
        }

        private void RefreshProductData()
        {

            if (!string.IsNullOrEmpty(SKU))
            {
                if(_productData.SKU != SKU)
                    _productData = inventoryBase.FindItemRecordBySKU(SKU);
            }
            if (_productData == null && !string.IsNullOrEmpty(ItemName))
            {
                _productData = inventoryBase.FindItemRecord(ItemName, _color, _sizeInInches);
            }
            if (_productData != null && !string.IsNullOrEmpty(SKU))
            {
                ItemName = _productData.ItemName;
                _color = _productData.Color;
                _sizeInInches = _productData.Size;
            }

        }

        private InventoryProduct _productData;
        public InventoryProduct ProductData {
            get
            {
                if (_productData == null)
                {
                    RefreshProductData();
                }
                return _productData;
            }
            set
            {
                _productData = value;
            }
        }
        public int Quantity { get; set; }
        private string _color;
        public string Color {
            get
            {
                 return _color;
            }
            set
            {
                _color = value;
                RefreshProductData();
            }
        }
        private int _sizeInInches;
        public int SizeInInches {
            get
            {
                return _sizeInInches;
            }
            set
            {
                _sizeInInches = value;
                RefreshProductData();
            }
        }

        public double ItemPrice { get; set; }
        public double ItemPriceQuantity { get; set; } = 1;

        public string SKU { get; set; }

        public bool IsDigital { get; set; }
        public double TotalPrice => ItemPrice * Quantity / ItemPriceQuantity;
        
        public bool Custom { get; set; }
        //TODO: public bool RushShipping { get; set; }

        //used as the short description
        public string GetDescription(bool longDescription = false)
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
            if (!string.IsNullOrEmpty(SKU))
            {
               // Options.Add("SKU: " + SKU.ToString());
            }
            if (!string.IsNullOrEmpty(Personalization))
            {
                var personalizationLabel = longDescription ? "Personalization" : "P";
                if (!longDescription && Personalization.Length > 30)
                {
                    Options.Add($"{personalizationLabel}: " + Personalization.Substring(0, 30) + "");
                }
                else
                {
                    Options.Add($"{personalizationLabel}: " + PersonalizationWithSize + "");
                }
            }
            foreach(var item in CustomFields)
            {
                Options.Add($"{item.Key}: " + item.Value + "");
            }

            if (Options.Count > 0)
            {
                if (longDescription)
                {
                    baseName += "\r\n   " + string.Join("\r\n   ", Options.ToArray());

                }
                else
                {
                    baseName += " (" + string.Join(", ", Options.ToArray()) + ")";
                }
            }

            return baseName;
        }

        protected string getMatchingString(Match match)
        {
            return (WebUtility.HtmlDecode(match.Groups[1].ToString().Trim()));
        }
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
