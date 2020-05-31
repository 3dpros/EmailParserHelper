using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Mail;
using System.Collections.Specialized;
//using EmailParserBackend.ScriptingInterface;
using AirtableClientWrapper;
using AsanaAPI;
using System.Text.RegularExpressions;
//using AsanaNet;
//using AirtableApiClient;
namespace EmailParserHelper
{
    public class ParserMethods
    {
        public static bool AddOrder(NameValueCollection fields, ref List<string> log, bool dryRun = false)
        {
            fields.Add("NotificationEmailAddresses", dryRun ? "altbillington@gmail.com" : "altbillington@gmail.com, daniel.c.english@gmail.com");

            var automation = new Automation(dryRun);
            var result = automation.ProcessOrder(fields);
            log = automation.Log;
            return result;
        }

        public static bool CreateManualInventoryOrder(string componentName, int quantity)
        {
            var inventoryBase = new AirtableItemLookup();
            var auto = new Automation();
            var component = inventoryBase.GetComponentByName(componentName, false);
            if (component != null)
            {
                auto.GenerateInventoryRequest(component, quantity);
                return true;
            }
            return false;
        }

        public static bool CompleteInventoryOrder(NameValueCollection fields, ref List<string> log, bool dryRun = false)
        {
            try
            {
                var match = Regex.Match(fields["Task Name"], @"\((\d+)\s*\/\s*(\d+)\)\s*(.*)");
                var quantityCompleted = int.Parse(match.Groups[1].Value);
                var quantityRequested = int.Parse(match.Groups[2].Value);
                var componentName = match.Groups[3].Value;

                var inventoryBase = new AirtableItemLookup();
                var component = inventoryBase.GetComponentByName(componentName);

                var auto = new Automation();
                auto.CompleteInventoryRequest(component, quantityCompleted, quantityRequested);
                return true;
            }
            catch (Exception e)
            {
                log.Add("Exception Occurred:" + e.Message + e.ToString());
                fields.Add("FailReason", e.Message + e.ToString());
                return false;
            }
    }
        public static bool AsanaNotification(NameValueCollection fields, ref List<string> log)
        {
            var emails = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("Al Billington","altbillington@gmail.com"),
                    //new KeyValuePair<string, string>("Kyle Perkuhn","3dproskyle@gmail.com"),
                    new KeyValuePair<string, string>("Daniel English","daniel.c.english@gmail.com"),
                };
            var emailsToNotify = new List<string>();
            var commentMatches = Regex.Match(fields["LastComment"], "(.*) added a comment.(.*) -").Groups;
            var commenter = commentMatches[1].Value;
            fields["LastComment"] = commentMatches[2].Value;

            //remove weird hyperlink that is put where the @mention should be, if present
            if (fields["LastComment"].Contains("list"))
            {
                fields["LastComment"] = Regex.Match(fields["LastComment"], "list (.*)").Groups[1].Value;
            }

            foreach (var pair in emails)
            {
                if (fields["BodyHTML"].Contains("@" + pair.Key))
                {
                    if (!commenter.ToLowerInvariant().Contains(pair.Key.ToLowerInvariant()))
                    {
                        emailsToNotify.Add(pair.Value);
                    }
                }
            }
            fields["NotificationEmail"] = string.Join(", ", emailsToNotify.ToArray());


            return emailsToNotify.Count > 0;
        }

    }
    public class Automation
    {
        private const string HighPriorityProjectName = "AHigh Priority";

        private AirtableItemLookup inventoryBase = new AirtableItemLookup();
        private AirtableOrders ATbase = new AirtableOrders();
        bool dryRun;
        private string etsyOrdersBoardName;
        AsanaWrapper Asana;
        public List<string> Log = new List<string>();

        public Automation(bool testing = false)
        {
            etsyOrdersBoardName = dryRun ? "Etsy Orders (Test)" : "Etsy Orders";
            Asana = new AsanaWrapper("0/7874fd8b3c16d982812217283e75c450", "3D Pros", etsyOrdersBoardName);
            dryRun = testing;
        }

        public bool ProcessOrder(NameValueCollection fields)
        {
            Log.Add("Starting Airtable Entry (v2)");

            try
            {
                var channel = fields["Channel"].ToLower();
                bool UseSKUsForItemLookup;
                Log.Add("parsing order using channel: " + channel);

                Order orderData;
                if (channel == "shopify" || channel == "amazon")
                {
                    orderData = new EmailParserHelper.ShopifyOrder(fields["body"]);
                    UseSKUsForItemLookup = true;
                }
                else if (channel == "etsy")
                {
                    orderData = new EmailParserHelper.EtsyOrder(fields["body"]);
                    UseSKUsForItemLookup = false;
                }
                else
                {
                    throw new Exception("Unknown channel specified: " + channel);
                }

                var productOrdersBoardName = dryRun ? "Etsy Orders (Test)" : "Etsy Orders";
                var orderID = fields["Order ID"];
                //only create if it doesn't yet exist
                if (string.IsNullOrEmpty(orderID))
                {
                    fields.Add("FailReason", "Invalid or empty order ID");
                    Log.Add("Order already exists in airtable:" + fields["Short Description"]);
                    return false;
                }
                Log.Add("Checking Order ID: " + orderID);
                if (dryRun || ATbase.GetRecordByOrderID(orderID, out _) == null)
                {
                    Log.Add("Populating Order");

                    var owner = "";
                    var DescriptionAddendum = ""; //hack until description is migrated from email parser

  
                    var hasCustomComponents = false;
                    var hasInventoryComponents = false;


                    if (!string.IsNullOrEmpty(orderData.ShortDescription))
                    {
                        //for now manually generated orders dont have a short description from the helper, use the email parser one
                        fields["Short Description"] = (dryRun ? "(TEST)" : "") + orderData.ShortDescription;
                    }
                    // Iterate over each transaction to check for inventory items

                    double totalMaterialCost = 0;
                    var transactionRecords = new List<TransactionRecord>();

                    foreach (var transaction in orderData.Transactions)
                    {
                        TransactionTypes currentTransactionType;
                        InventoryProduct currentProductData;
                        if (UseSKUsForItemLookup)
                        {
                            currentProductData = inventoryBase.FindItemRecordBySKU(transaction.SKU);
                        }
                        else
                        {
                            currentProductData = inventoryBase.FindItemRecord(transaction.ItemName, transaction.Color, transaction.SizeInInches);
                        }
                        if(transaction.ProductData != currentProductData)
                        {
                            Log.Add("ASSERT: order productdata does not match addOrder productData: " + transaction.ItemName);
                        }
                        if (currentProductData == null)
                        {
                            //if any item in the order is custom, the whole order is custom
                            hasCustomComponents = true;
                            currentTransactionType = TransactionTypes.UntrackedCustom;
                            Log.Add("Found Untracked Custom Item: " + transaction.ItemName);
                        }
                        else
                        {
                            // add a link to the internal designer with the design code
                            if (!string.IsNullOrEmpty(currentProductData.BaseUrl))
                            {
                                DescriptionAddendum += "\n" + currentProductData.BaseUrl;
                                // ditching this until asana API supports special characters
                                //if (!string.IsNullOrEmpty(transaction.DesignerUrlFull))
                                // {
                                //    if (transaction.SizeInInches != 0)
                                   // {
                                       // DescriptionAddendum += "|sizeOpt=" + transaction.SizeInInches.ToString();
                                   // }
                                //    log.Add("found designer code: URL is " + DescriptionAddendum);
                                //}
                                //else
                                //{
                                //if the personalization isn't a design code, link the base designer page anyway for convenience   
                                Log.Add("item has a designer: URL is " + DescriptionAddendum);
                                // }
                            }
                            if (currentProductData.IsInventory() 
                                && !transaction.Custom 
                                && transaction.Quantity < currentProductData.MaximumInventoryQuantity)
                            {
                                hasInventoryComponents = true;
                                currentTransactionType = TransactionTypes.Inventory;
                                Log.Add("Found Inventory Item: " + transaction.ItemName);
                            }
                            else
                            {
                                Log.Add("Found Tracked Custom Item: " + transaction.ItemName + " (" + currentProductData.ItemName + ")");
                                currentTransactionType = TransactionTypes.TrackedCustom;
                                hasCustomComponents = true;
                            }

                            //the material cost of the order is the sum of the material cost for all items in the order
                            totalMaterialCost += currentProductData.MaterialCost * transaction.Quantity;

                        }

                        // add all transactions to a list including untracked items
                        transactionRecords.Add(new TransactionRecord()
                        {
                            Transaction = transaction,
                            Record = currentProductData,
                            TransactionType = currentTransactionType
                        });
                    }

                    // determine the list of potential printers for the order. this is ther intersection of the potential printers 
                    HashSet<string> printersForOrderHashSet = null;
                    string preferredPrinter = "";
                    foreach (var transactionRecord in transactionRecords)
                    {
                        if (transactionRecord.TransactionType != TransactionTypes.UntrackedCustom)
                        {
                            List<string> printersForProduct;
                            inventoryBase.GetPotentialPrintersList(transactionRecord.Record, out printersForProduct, out preferredPrinter);
                            //ignore if printers in empty, which means anyone can print the product
                            if (printersForProduct != null && printersForProduct.Count > 0)
                            {
                                if (printersForOrderHashSet == null)
                                {
                                    printersForOrderHashSet = new HashSet<string>(printersForProduct);
                                }
                                else
                                {
                                    printersForOrderHashSet.IntersectWith(printersForProduct);
                                }
                            }
                        }
                    }
                    var potentialPrinters = printersForOrderHashSet?.ToArray();
                    var printersString = GetPrinterDetailsString(ref owner, potentialPrinters, preferredPrinter);
                    Log.Add(printersString);
                    DescriptionAddendum += "\n\n" + printersString;

                    Log.Add("Has Custom Components:" + (hasCustomComponents ? "yes" : "no"));
                    Log.Add("Has Inventory Components:" + (hasInventoryComponents ? "yes" : "no"));

                    Log.Add("Total Material Cost:" + totalMaterialCost);
                    Log.Add("Adding Order " + orderID.ToString());
                    var projects = new List<string>() { productOrdersBoardName };

                    foreach (var transactionRecord in transactionRecords)
                    {
                        if (transactionRecord.TransactionType != TransactionTypes.UntrackedCustom)
                        {
                            List<InventoryComponent> components = new List<InventoryComponent>();
                            if (!dryRun)
                            {
                                inventoryBase.UpdateInventoryCountForTransaction(transactionRecord.Record, transactionRecord.Transaction.Quantity, out components, orderID);
                            }
                            else
                            {
                               // components = transactionRecord.Record.
                            }                                
                            foreach (InventoryComponent component in components)
                            {
                                if (component.IsBelowThreshhold())
                                {
                                    GenerateInventoryRequest(component);
                                }
                            }
                        }
                    }

                    var order = ATbase.newOrderData(orderID);
                    //used to ensure that the printer operator is paid only for the custom portion of an order
                    order.ValueOfInventory = (from record in transactionRecords
                                              where record.TransactionType == TransactionTypes.Inventory
                                              select record.Transaction.TotalPrice).Sum();
                    order.MaterialCost = totalMaterialCost;
                    //TODO: implement this in this library and not in an email parser step
                    order.Notes = ((channel == "etsy")?orderData.LongDescription: fields["Long Description"]) + DescriptionAddendum;
                    order.ShippingCharge = Double.Parse(fields["Shipping Charge"]);
                    order.TotalPrice = Double.Parse(fields["Order Total"]);
                    order.Description = fields["Short Description"];
                    order.Channel = fields["Channel"];
                    order.DueDate = orderData.DueDate;
                    order.Rush = (order.ShippingCharge > 0 && order.ShippingCharge < 12);
                    order.SalesTax = Double.Parse(fields["Sales Tax"]);
                    order.CustomerEmail = fields["Customer Email"];

                    //used for notification emails to show actual profit
                    fields.Add("Profit", (order.TotalPrice - order.ShippingCharge).ToString("C"));

                    if (!hasCustomComponents && hasInventoryComponents)
                    {
                        projects.Add("Shipper");
                        owner = "Zapier";
                    }
                    if (hasCustomComponents && hasInventoryComponents)
                    {
                        projects.Add("Mixed Order");
                        var inventoryPackingList = new List<string>() { "---Inventory packing list---" };
                        var customPackingList = new List<string>() { "---Custom item packing list---" };

                        foreach (var item in transactionRecords)
                        {
                            if (item.TransactionType == TransactionTypes.Inventory)
                            {
                                inventoryPackingList.Add(item.GetDescription());
                            }
                            else if (item.TransactionType == TransactionTypes.TrackedCustom || item.TransactionType == TransactionTypes.UntrackedCustom)
                            {
                                customPackingList.Add(item.GetDescription());
                            }
                        }
                        order.Notes += "\r\n\r\n" + string.Join("\r\n", inventoryPackingList.ToArray()) + "\r\n\r\n" + string.Join("\r\n", customPackingList.ToArray());
                    }
                    if (order.Rush)
                    {
                        projects.Add(HighPriorityProjectName);
                        order.Description = "[Priority]" + order.Description;
                    }

                    if (!dryRun)
                    {
                        var asanaTask = Asana.AddTask(order.Description, order.Notes, projects, order.DueDate, owner);
                        order.AsanaTaskID = asanaTask.ID.ToString();
                        ATbase.CreateOrderRecord(order);
                    }
                    else
                    {
                        var asanaTask = Asana.AddTask(order.Description, order.Notes, new List<string>() { productOrdersBoardName }, order.DueDate, owner);
                    }
                }
                else
                {
                    fields.Add("FailReason", "Order already in airtable");
                    Log.Add("Order already exists in airtable:" + fields["Short Description"]);
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Add("Exception Occurred:" + e.Message + e.ToString());
                fields.Add("FailReason", e.Message + e.ToString());
                return false;
            }
            return true;
        }

        public void CompleteInventoryRequest(InventoryComponent component, int quantityCompleted, int quantityRequested)
        {
            var inventoryBase = new AirtableItemLookup();
            Log.Add("Inventory order completed, updating counts");
            Log.Add("Update count of " + component.Name + " by +" + quantityCompleted.ToString());
            Log.Add("Original request was for " + quantityRequested.ToString());

            inventoryBase.UpdateComponentQuantity(component, quantityCompleted, quantityRequested);
        }

        /// <summary>
        /// generate an inventory request using quantity determined by the component metadata
        /// </summary>
        public void GenerateInventoryRequest(InventoryComponent component)
        {
            GenerateInventoryRequestCore(component, component.BatchSize, component.NumberOfBatches);
        }

        /// <summary>
        /// generate an inventory request for an arbitrary quantity
        /// </summary>
        public void GenerateInventoryRequest(InventoryComponent component, int quantity)
        {
            GenerateInventoryRequestCore(component, quantity, 1, "(generated manually)");
        }
        private void GenerateInventoryRequestCore(InventoryComponent component, int quantityPerBatch, int batches, string noteAddendum = "")
        {
            for (int i = 0; i < batches; i++)
            {
                inventoryBase.LogInventoryRequestCreation(component, quantityPerBatch);
                string cardName = $"(0/{quantityPerBatch.ToString()}) {component.Name}";
                DateTime dueDate = DateTime.Today.AddDays(10);

                var inventoryOrder = ATbase.newOrderData("I_" + Guid.NewGuid().ToString());
                inventoryOrder.Channel = "Internal";
                inventoryOrder.Description = cardName;
                inventoryOrder.DueDate = dueDate;
                inventoryOrder.TotalPrice = component.InternalPrice * quantityPerBatch;
                List<string> printerNames;
                string preferredInvPrinter;
                string inventoryOrderOwner = "";

                inventoryBase.GetPotentialPrintersList(component, out printerNames, out preferredInvPrinter);
                var inventoryOrderPrintersString = GetPrinterDetailsString(ref inventoryOrderOwner, printerNames.ToArray(), preferredInvPrinter);
                inventoryOrder.Notes = "available quantity when this card was created: " + component.Quantity.ToString() +
                    "\nnumber pending when order was created: " + component.Pending +
                    "\n" + component.Details +
                    "\n" + inventoryOrderPrintersString+
                    "\n\n" + noteAddendum;

                var inventoryTrackingBoardName = component.IsExternal ? "Parts Order Requests" : "Inventory Requests";
                var orderProjects = new List<string>() { etsyOrdersBoardName, inventoryTrackingBoardName };

                // mark as high priority if we don't have many in stock
                if ((component.Quantity + i * quantityPerBatch) < (quantityPerBatch * batches) / 2)
                {
                    orderProjects.Add(HighPriorityProjectName);
                }
                var invAsanaTask = Asana.AddTask(cardName, inventoryOrder.Notes, orderProjects, inventoryOrder.DueDate, inventoryOrderOwner);
                Log.Add("added task to asana" + cardName);
                if (!dryRun)
                {
                    inventoryOrder.AsanaTaskID = invAsanaTask.ID.ToString();
                    ATbase.CreateOrderRecord(inventoryOrder);
                }
            }
                     
        }

        private static string GetPrinterDetailsString(ref string owner, string[] potentialPrinters, string preferredPrinter)
        {
            if (potentialPrinters?.Length > 0)
            {
                var PrintersString = "Potential Printers: " + string.Join(", ", potentialPrinters);
                if (potentialPrinters.Length == 1)
                {
                    owner = potentialPrinters[0];
                }
                //only show preferred printer if there are multiple options
                else if (!string.IsNullOrEmpty(preferredPrinter))
                {
                    PrintersString += "\nPreferred Printer: " + preferredPrinter;
                }
                return PrintersString;
            }
            return "";
        }

        
        

    }

    public enum TransactionTypes
    {
        Inventory,
        TrackedCustom,
        UntrackedCustom

    }
    class TransactionRecord
    {


        public TransactionTypes TransactionType { set; get; }
        public Transaction Transaction { set; get; }
        public InventoryProduct Record { set; get; }


        public string GetDescription()
        {
            return Transaction.GetDescription();
        }
    }
}


