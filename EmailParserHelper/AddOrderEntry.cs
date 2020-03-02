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
            try
            {
                fields.Add("NotificationEmailAddresses", dryRun ? "altbillington@gmail.com" : "altbillington@gmail.com, daniel.c.english@gmail.com");

                log.Add("Starting Airtable Entry (v2)");

                var inventoryBase = new AirtableItemLookup();
                var ATbase = new AirtableOrders();

                var Asana = new AsanaWrapper("0/7874fd8b3c16d982812217283e75c450", "3D Pros", "Etsy Orders");
                
                var orderID = fields["Order ID"];
                var channel = fields["Channel"].ToLower();

                log.Add("Checking Order " + orderID);
                //only create if it doesn't yet exist
                if (dryRun || ATbase.GetRecordByOrderID(orderID, out _) == null)
                {
                    log.Add("Populating Order");

                    var owner = "";
                    
                    bool UseSKUsForItemLookup;
                    var hasCustomComponents = false;
                    var hasInventoryComponents = false;

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
                        ItemData currentRecord;
                        if (UseSKUsForItemLookup)
                        {
                            currentRecord = inventoryBase.FindItemRecordBySKU(transaction.SKU);
                        }
                        else
                        {
                            currentRecord = inventoryBase.FindItemRecord(transaction.ItemName, transaction.Color, transaction.SizeInInches);
                        }
                        if (currentRecord == null)
                        {
                            //if any item in the order is custom, the whole order is custom
                            hasCustomComponents = true;
                            currentTransactionType = TransactionTypes.UntrackedCustom;
                            log.Add("Found Untracked Custom Item: " + transaction.ItemName);
                        }
                        else
                        {
                            if (currentRecord.IsInventory() && !transaction.Custom)
                            {
                                hasInventoryComponents = true;
                                currentTransactionType = TransactionTypes.Inventory;
                                log.Add("Found Inventory Item: " + transaction.ItemName);
                            }
                            else
                            {
                                log.Add("Found Tracked Custom Item: " + transaction.ItemName);
                                currentTransactionType = TransactionTypes.TrackedCustom;
                                hasCustomComponents = true;
                            }

                            //the material cost of the order is the sum of the material cost for all items in the order
                            totalMaterialCost += currentRecord.GetMaterialCost() * transaction.Quantity;
                        }
                        transactionRecords.Add(new TransactionRecord() {
                            Transaction = transaction,
                            Record = currentRecord,
                            TransactionType = currentTransactionType });
                    }

                    log.Add("Has Custom Components:" + (hasCustomComponents ? "yes" : "no"));
                    log.Add("Has Inventory Components:" + (hasInventoryComponents ? "yes" : "no"));

                    log.Add("Total Material Cost:" + totalMaterialCost);
                    log.Add("Adding Order " + orderID.ToString());
                    var projects = new List<string>() { "Etsy Orders" };

                    foreach (var transactionRecord in transactionRecords)
                    {
                        List<ItemComponentData> components = new List<ItemComponentData>();
                        if (!dryRun)
                            inventoryBase.UpdateInventoryCountForTransaction(transactionRecord.Record, transactionRecord.Transaction.Quantity, out components, orderID);
                        foreach (ItemComponentData component in components)
                        {
                            //create a new inventory request if needed
                            if (component.IsBelowThreshhold())
                            {
                                //update already takes batch into account so only do it once
                                inventoryBase.UpdateComponentForInventoryRequest(component);

                                for (int i = 0; i < component.NumberOfBatches; i++)
                                {
                                    string cardName = $"({component.BatchSize.ToString()}/{component.BatchSize.ToString()}) {component.Name}";
                                    DateTime dueDate = DateTime.Today.AddDays(10);

                                    var inventoryOrder = ATbase.newOrderData("I_" + Guid.NewGuid().ToString());
                                    inventoryOrder.Channel = "Internal";
                                    inventoryOrder.Description = cardName;
                                    inventoryOrder.DueDate = dueDate;
                                    if (!dryRun)
                                    {
                                        var invAsanaTask = Asana.AddTask(cardName, component.Details, new List<string>() { "Etsy Orders", "Inventory Requests" }, dueDate);
                                        inventoryOrder.AsanaTaskID = invAsanaTask.ID.ToString();
                                        ATbase.CreateOrderRecord(inventoryOrder);
                                    }
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
                    order.Notes = fields["Long Description"];
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
                                inventoryPackingList.Add(item.ToString());
                            }
                            else if (item.TransactionType == TransactionTypes.TrackedCustom || item.TransactionType == TransactionTypes.UntrackedCustom)
                            {
                                customPackingList.Add(item.ToString());
                            }
                        }
                        order.Notes += "\r\n\r\n" + string.Join("\r\n", inventoryPackingList.ToArray()) + "\r\n\r\n" + string.Join("\r\n", customPackingList.ToArray());
                    }
                    if (order.Rush)
                    {
                        projects.Add("AHigh Priority");
                        order.Description = "[Priority]" + order.Description;
                    }

                    if (!dryRun)
                    {
                        var asanaTask = Asana.AddTask(order.Description, order.Notes, projects, order.DueDate, owner);
                        order.AsanaTaskID = asanaTask.ID.ToString();
                        ATbase.CreateOrderRecord(order);
                    }
                }
                else
                {
                    fields.Add("FailReason", "Order already in airtable");
                    log.Add("Order already exists in airtable:" + fields["Short Description"]);
                    return false;
                }
            }
            catch (Exception e)
            {
                log.Add("Exception Occurred:" + e.Message + e.ToString());
                fields.Add("FailReason", e.Message + e.ToString());
                return false;
            }
            return true;
        }

        public static bool CompleteInventoryOrder(NameValueCollection fields, ref List<string> log, bool dryRun = false)
        {
            try
            {

                var inventoryBase = new AirtableItemLookup();
                log.Add("Inventory order, update counts");
                // var match = Regex.Match(fields["Task Name"], @"\((\d+)x\)\s*(.*)");
                var match = Regex.Match(fields["Task Name"], @"\((\d+)\s*\/\s*(\d+)\)\s*(.*)");
                log.Add("match 1");
                var quantity = int.Parse(match.Groups[1].Value);
                var originalQuantity = int.Parse(match.Groups[2].Value);
                var componentName = match.Groups[3].Value;
                log.Add("Inventory order, update count of " + componentName + " by +" + quantity.ToString());
                inventoryBase.UpdateComponentQuantityByName(componentName, quantity, originalQuantity);

            }
            catch (Exception e)
            {
                log.Add("Exception Occurred:" + e.Message + e.ToString());
                fields.Add("FailReason", e.Message + e.ToString());
                return false;
            }
            return true;
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
        public ItemData Record { set; get; }
        public override string ToString()
        {
            return Transaction.ToString();
        }
    }
}


