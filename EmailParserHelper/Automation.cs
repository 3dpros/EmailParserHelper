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

        public bool ProcessOrder(string emailBody, string channel, out Order orderData)
        {
            Log.Add("Starting Airtable Entry (v2)");
            channel = channel.ToLower();
            bool UseSKUsForItemLookup;
            Log.Add("parsing order using channel: " + channel);

            if (channel == "shopify" || channel == "amazon")
            {
                orderData = new EmailParserHelper.ShopifyOrder(emailBody);
                UseSKUsForItemLookup = true;
            }
            else if (channel == "etsy")
            {
                orderData = new EmailParserHelper.EtsyOrder(emailBody);
                UseSKUsForItemLookup = false;
            }
            else
            {
                throw new Exception("Unknown channel specified: " + channel);
            }

            var productOrdersBoardName = dryRun ? "Etsy Orders (Test)" : "Etsy Orders";
            var orderID = orderData.OrderID;
            //only create if it doesn't yet exist
            if (string.IsNullOrEmpty(orderID))
            {
                var errorMsg = "Order already exists in airtable:" + orderData.ShortDescription;
                Log.Add(errorMsg);
                throw new System.Exception(errorMsg);
            }
            Log.Add("Checking Order ID: " + orderID);
            if (dryRun || ATbase.GetRecordByOrderID(orderID, out _) == null)
            {
                Log.Add("Populating Order");

                var owner = "";
                var DescriptionAddendum = ""; //hack until description is migrated from email parser

  
                var hasCustomComponents = false;
                var hasInventoryComponents = false;

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
                        Log.Add("ASSERT: order productdata does not match addOrder productData: " + transaction.ItemName + ", transaction data: " + currentProductData?.ItemName);
                    }
                    if (currentProductData == null)
                    {
                        //if any item in the order is custom, the whole order is custom
                        hasCustomComponents = true;
                        currentTransactionType = TransactionTypes.UntrackedCustom;
                        inventoryBase.AddProductRecord(transaction.ItemName);
                        Log.Add("Found Untracked Custom Item: " + transaction.ItemName + ". Adding it to catalog.");
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

                var airtableOrder = ATbase.newOrderData(orderID);
                //used to ensure that the printer operator is paid only for the custom portion of an order
                airtableOrder.ValueOfInventory = (from record in transactionRecords
                                            where record.TransactionType == TransactionTypes.Inventory
                                            select record.Transaction.TotalPrice).Sum();
                airtableOrder.MaterialCost = totalMaterialCost;
                //TODO: implement this in this library and not in an email parser step
                airtableOrder.Notes = orderData.LongDescription + DescriptionAddendum;
                airtableOrder.ShippingCharge = orderData.ShippingCharge;
                airtableOrder.TotalPrice = orderData.OrderTotal;
                airtableOrder.Description = orderData.ShortDescription;
                airtableOrder.Channel = channel;
                airtableOrder.DueDate = orderData.DueDate;
                airtableOrder.Rush = (airtableOrder.ShippingCharge > 0 && airtableOrder.ShippingCharge < 12);
                airtableOrder.SalesTax = orderData.SalesTax;
                airtableOrder.CustomerEmail = orderData.Customer.Email;

                //used for notification emails to show actual profit

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
                    airtableOrder.Notes += "\r\n\r\n" + string.Join("\r\n", inventoryPackingList.ToArray()) + "\r\n\r\n" + string.Join("\r\n", customPackingList.ToArray());
                }
                if (airtableOrder.Rush)
                {
                    projects.Add(HighPriorityProjectName);
                    airtableOrder.Description = "[Priority]" + airtableOrder.Description;
                }
                Log.Add("assigning to owner: " + owner);
                airtableOrder.PrintOperator = owner;

                if (!dryRun)
                {
                    var asanaTask = Asana.AddTask(airtableOrder.Description, airtableOrder.Notes, projects, airtableOrder.DueDate, owner);
                    airtableOrder.AsanaTaskID = asanaTask.ID.ToString();
                    ATbase.CreateOrderRecord(airtableOrder);
                }
                else
                {
                    var asanaTask = Asana.AddTask(airtableOrder.Description, airtableOrder.Notes, new List<string>() { productOrdersBoardName }, airtableOrder.DueDate, owner);
                }
            }
            else
            {
                var errorMsg = "Order already exists in airtable:" + orderData.ShortDescription;
                Log.Add(errorMsg);
                throw new Exception(errorMsg);
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
            var pendingBeforeStart = component.Pending;

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
                    "\nnumber pending when card was created: " + pendingBeforeStart + "->" + component.Pending +
                    "\n" + component.Details +
                    "\n" + inventoryOrderPrintersString+
                    "\n\n" + noteAddendum;

                var orderProjects = new List<string>() { etsyOrdersBoardName, "Inventory Requests" };
                if( component.IsExternal)
                {
                        orderProjects.Add("Parts Order Requests");
                }
                // mark as high priority if we don't have many in stock
                if ((component.Quantity + i * quantityPerBatch) < (quantityPerBatch * batches) / 2)
                {
                    orderProjects.Add(HighPriorityProjectName);
                }
                inventoryOrder.PrintOperator = inventoryOrderOwner;
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
            var PrintersString = "";
            if (potentialPrinters?.Length > 0)
            {
                PrintersString = "Potential Printers: " + string.Join(", ", potentialPrinters);
                if (potentialPrinters.Length == 1)
                {
                    owner = potentialPrinters[0];
                }
            }
            //only show preferred printer if there are multiple options
            else if (!string.IsNullOrEmpty(preferredPrinter))
            {
                PrintersString += "\nAuto-assigning to Preferred Printer: " + preferredPrinter;
                owner = preferredPrinter;
            }              
            return PrintersString;            
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


