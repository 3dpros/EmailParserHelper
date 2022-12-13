using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Mail;
using System.Collections.Specialized;
using AirtableClientWrapper;
using System.Text.RegularExpressions;
using EmailParserHelper.Expenses;
namespace EmailParserHelper
{

    public class Automation
    {
        readonly string defaultShipper = "James English";
        readonly string warehouseManager = "Daniel English";

        private AirtableItemLookup ATItemLookupBase = new AirtableItemLookup();
        private AirtableOrders ATOrdersBase;
        private AirtableOrderTracking ATTrackingBase = new AirtableOrderTracking();
        private AirtableInventory ATInventory = new AirtableInventory();

        AirtableTransactions TransactionsBase = new AirtableTransactions();


        bool dryRun;
        private string etsyOrdersBoardName;
        public List<string> Log = new List<string>();

        public Automation(bool testing = false)
        {
            etsyOrdersBoardName = dryRun ? "Etsy Orders (Test)" : "Etsy Orders";
            dryRun = testing;
            ATOrdersBase = new AirtableOrders(testing);
        }

        public Order getOrderData(string emailBody, string emailHTML, string channel, out bool UseSKUsForItemLookup)
        {
            EmailParserHelper.Order orderData;
            var channelKey = channel.ToLower();
            Log.Add("parsing order using channel: " + channelKey);

            if (channelKey == "shopify" || channelKey == "amazon")
            {
                orderData = new EmailParserHelper.ShopifyOrder(emailBody);
                UseSKUsForItemLookup = true;
            }
            else if (channelKey == "etsy")
            {
                orderData = new EtsyOrder(emailBody, emailHTML);
                UseSKUsForItemLookup = false;
            }
            else
            {
                throw new Exception("Unknown channel specified: " + channelKey);
            }
            return orderData;
        }

        public bool ProcessOrderFromFields(NameValueCollection fields, string channel, out Order orderData, out OrderTrackingData orderTracking)
        {
            //etsy only currently
            orderData = new EtsyOrder(fields);
            return ProcessOrder(channel, orderData, false, out orderTracking);
        }

        public bool ProcessOrder(string emailBody, string emailHTML, string channel, out Order orderData, out OrderTrackingData orderTracking)
        {
            orderData = getOrderData(emailBody, emailHTML, channel, out bool useSKUsForItemLookup);
            return ProcessOrder(channel, orderData, useSKUsForItemLookup, out orderTracking);
        }

        public bool ProcessOrder(string channel, Order orderData, bool UseSKUsForItemLookup, out OrderTrackingData orderTracking)
        {
            orderTracking = null;
            var channelKey = channel.ToLower();

            Log.Add("Starting Airtable Entry (v2)");

            var productOrdersBoardName = dryRun ? "Etsy Orders (Test)" : "Etsy Orders";
            var orderID = orderData.OrderID;
            if (string.IsNullOrEmpty(orderID))
            {
                var errorMsg = "Order already exists in airtable:" + orderData.OneLineDescription;
                Log.Add(errorMsg);
                throw new System.Exception(errorMsg);
            }
            Log.Add("Checking Order ID: " + orderID);
            //only create if it doesn't yet exist
            if (dryRun || ATOrdersBase.GetRecordByOrderID(orderID, out _) == null)
            {
                Log.Add("Populating Order");

                var owner = "";
                var DescriptionAddendum = ""; //hack until description is migrated from email parser
                var DesignerURL = ""; //hack until description is migrated from email parser
                var Personalization = "";

                var hasCustomComponents = false;
                var hasInventoryComponents = false;

                // Iterate over each transaction to check for inventory items

                double totalMaterialCost = 0;
                var transactionRecordPairs = new List<TransactionRecordPair>();

                foreach (var transaction in orderData.Transactions)
                {
                    TransactionTypes currentTransactionType;
                    InventoryProduct currentProductData;
                    if (UseSKUsForItemLookup)
                    {
                        currentProductData = ATItemLookupBase.FindItemRecordBySKU(transaction.SKU);
                    }
                    else
                    {
                        currentProductData = ATItemLookupBase.FindItemRecord(transaction.ItemName, transaction.Color, transaction.SizeInInches);
                    }
                    if (transaction.ProductData?.UniqueName != currentProductData?.UniqueName)
                    {
                        Log.Add("ASSERT: order productdata does not match addOrder productData: " + transaction.ProductData?.UniqueName + ", transaction data: " + currentProductData?.UniqueName);
                    }
                    if (currentProductData == null)
                    {
                        //if any item in the order is custom, the whole order is custom
                        hasCustomComponents = true;
                        currentTransactionType = TransactionTypes.UntrackedCustom;
                        //only add to the catalog for etsy orders since shopify orders dont have size or color info
                        if (channelKey == "etsy")
                        {
                            currentProductData = ATItemLookupBase.AddProductRecord(transaction.CleanedItemName, transaction.Color, transaction.SizeInInches);
                            transaction.ProductData = currentProductData;
                            Log.Add($"Found Untracked Custom Item: {transaction.CleanedItemName} {transaction.Color} {transaction.SizeInInches}. Adding it to catalog.");
                        }
                    }
                    else
                    {

                        // add a link to the internal designer with the design code
                        if (!string.IsNullOrEmpty(currentProductData.BaseUrl))
                        {
                            DescriptionAddendum += "\n" + currentProductData.BaseUrl;
                            DesignerURL = currentProductData.BaseUrl;
                            if (!string.IsNullOrEmpty(transaction.DesignerUrlFull))
                            {
                                DesignerURL = transaction.DesignerUrlFull;                              
                            }
                            else
                            {
                                //if the personalization isn't a design code, link the base designer page anyway for convenience   
                                Log.Add("item has a designer: URL is " + DesignerURL);
                            }
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
                    transactionRecordPairs.Add(new TransactionRecordPair()
                    {
                        Transaction = transaction,
                        Product = currentProductData,
                        TransactionType = currentTransactionType
                    });
                    if (transaction.HumanReadablePersonalization != "")
                    {
                        Personalization = transaction.HumanReadablePersonalization;
                    }
                }

                //if the order has only one product, update the product DB with the image
                if (orderData.Transactions.Count == 1 && !string.IsNullOrEmpty(orderData.ImageURL))
                {
                    Log.Add("updating product image URL" + orderData.ImageURL);
                    ATItemLookupBase.AddImageToProduct(orderData.Transactions[0].ProductData, orderData.ImageURL);
                }

                // determine the list of potential printers for the order. this is ther intersection of the potential printers 
                HashSet<string> printersForOrderHashSet = null;
                string preferredPrinter = "";
                foreach (var transactionRecord in transactionRecordPairs)
                {
                    if (transactionRecord.TransactionType != TransactionTypes.UntrackedCustom)
                    {
                        List<string> printersForProduct;
                        ATItemLookupBase.GetPotentialPrintersList(transactionRecord.Product, out printersForProduct, out preferredPrinter);
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
                string preferredShipper = defaultShipper;

                //currently, always assign orders to the default shipper unless all transactions are specifically assigned to a single alternate shipper
                foreach (var transactionRecord in transactionRecordPairs)
                {
                    if(transactionRecord.TransactionType == TransactionTypes.UntrackedCustom)
                    {
                        preferredShipper = defaultShipper;
                        break;
                    }
                    else
                    {
                        ATItemLookupBase.GetPreferredShipper(transactionRecord.Product, out preferredShipper);
                        if (preferredShipper == defaultShipper)
                        {
                            break;
                        } 
                        else if (string.IsNullOrEmpty(preferredShipper))
                        {
                            preferredShipper = defaultShipper;
                            break;
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

                //handle inventory counts and request generation
                foreach (var transactionRecord in transactionRecordPairs)
                {
                    if (transactionRecord.TransactionType != TransactionTypes.UntrackedCustom)
                    {
                        List<InventoryComponent> components = new List<InventoryComponent>();
                        if (!dryRun)
                        {
                            // inventoryBase.UpdateInventoryCountForTransaction(transactionRecord.Product, transactionRecord.Transaction.Quantity, out components, orderID);
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

                var airtableOrder = ATOrdersBase.newOrderData(orderID);
                //used to ensure that the printer operator is paid only for the custom portion of an order
                airtableOrder.ValueOfInventory = (from record in transactionRecordPairs
                                                  where record.TransactionType == TransactionTypes.Inventory
                                                  select record.Transaction.TotalPrice).Sum();
                airtableOrder.MaterialCost = totalMaterialCost;
                airtableOrder.Notes = orderData.LongDescription + DescriptionAddendum;
                airtableOrder.OrderNote = orderData.Notes;
                airtableOrder.ShippingCharge = orderData.ShippingCharge;
                airtableOrder.TotalPrice = orderData.OrderTotal;
                airtableOrder.Description = (dryRun ? "(TEST)" : "") + orderData.ShortDescription;
                airtableOrder.Channel = channel;
                airtableOrder.DueDate = orderData.DueDate;
                airtableOrder.Rush = (airtableOrder.ShippingCharge > 5 && airtableOrder.ShippingCharge < 17);
                airtableOrder.SalesTax = orderData.SalesTax;
                airtableOrder.CustomerEmail = orderData.Customer.Email;
                airtableOrder.OrderURL = orderData.OrderUrl;
                airtableOrder.Shipper = preferredShipper;

                var startingOrderStage = "Assigned";

                if (hasCustomComponents && hasInventoryComponents)
                {
                    var inventoryPackingList = new List<string>() { "---Inventory packing list---" };
                    var customPackingList = new List<string>() { "---Custom item packing list---" };

                    foreach (var item in transactionRecordPairs)
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

                //order is completely inventory products
                if (!hasCustomComponents && hasInventoryComponents)
                {
                    Log.Add("full inventory order");
                    startingOrderStage = "Ship";
                    airtableOrder.PrintOperator = "Inventory";
                }
                else if (!string.IsNullOrEmpty(owner))
                {
                    Log.Add("assigning to owner: " + owner);
                    airtableOrder.PrintOperator = owner;
                    startingOrderStage = "Assigned";
                }
                if (airtableOrder.Rush)
                {
                    airtableOrder.Description = "[Priority]" + airtableOrder.Description;
                }

                string orderRecordId = string.Empty;
                if (!orderData.DigitalOrder)
                {
                    orderTracking = ATTrackingBase.OrderDataToOrderTrackingData(airtableOrder);
                    orderTracking.IncludedItems = (from txns in orderData.Transactions
                                                   where txns.ProductData != null
                                                   select txns?.ProductData.UniqueName.Trim())?.Distinct().ToList();

                    orderTracking.OrderURL = orderData.OrderUrlMarkdown;
                    orderTracking.DesignerURL = DesignerURL;
                    orderTracking.Personalization = Personalization;
                    orderTracking.Stage = startingOrderStage;
                    orderTracking.OrderNote = orderData.Notes;
                    ATTrackingBase.CreateOrderRecord(orderTracking, out orderRecordId);

                    Log.Add("created order in order tracking base, record ID: " + orderRecordId);
                }
                else
                {
                    Log.Add("no tracking entry needed, order is digital");
                    airtableOrder.ShipDate = DateTime.Now;
                }
                ATOrdersBase.CreateOrderRecord(airtableOrder);
                Log.Add("created order in main base");
                if (!orderData.DigitalOrder)
                {
                    //need to loop again here after the order is created so the order record can be linked to the txn records
                    foreach (var txn in orderData.Transactions)
                    {
                        if (txn.ProductData != null)
                        {
                            var transactionRecord = TransactionsBase.NewTransactionData(txn.ProductData);
                            transactionRecord.Quantity = txn.Quantity;
                            transactionRecord.OrderID = orderRecordId;
                            transactionRecord.Paid = txn.ItemPrice;

                            TransactionsBase.CreateOrderRecord(transactionRecord);
                            Log.Add("created transaction record for " + txn.ProductData.UniqueName);
                        }
                    }
                }

            }
            else
            {
                var errorMsg = "Order already exists in airtable:" + orderData.OneLineDescription;
                Log.Add(errorMsg);
                throw new Exception(errorMsg);
            }
            return true;
        }

        public bool BackfillShippingCost(string orderID, string shippingCost)
        {
            Log.Add("Starting Airtable Entry - backfill shipping cost");
            Log.Add("Checking Order " + orderID);

            var airtableOrderRecord = ATOrdersBase.GetRecordByOrderID(orderID, out _);
            Log.Add("Got pay table Record");
            if (airtableOrderRecord != null)
            {
                airtableOrderRecord.ShippingCost = (!string.IsNullOrEmpty(shippingCost)) ? double.Parse(shippingCost) : 0;
                Log.Add("Setting actual shipping cost to " + airtableOrderRecord.ShippingCost.ToString());
                Log.Add("Order Exists, updating " + orderID.ToString());
                ATOrdersBase.CreateOrderRecord(airtableOrderRecord, true);

            }
            return true;
        }
        public bool CompleteOrder(string orderID, string shippingCost)
        {
            Log.Add("Starting Airtable Entry");

            //string orderID = fields["Order ID"];

            Log.Add("Checking Order " + orderID);
            var airtableOrderRecord = ATOrdersBase.GetRecordByOrderID(orderID, out _);
            Log.Add("Got pay table Record");
            var orderTrackingRecord = ATTrackingBase.GetRecordByOrderID(orderID, out _);
            Log.Add("found order tracking record: " + orderTrackingRecord.Description);

            if (airtableOrderRecord != null && orderTrackingRecord != null)
            {
                Log.Add("Record Exists");
                //If ship date is already set on the tracking record, dont process it again.  If not, it is likely a reship
                if (orderTrackingRecord.ShipDate.Year > 2000)
                {
                    Log.Add("Item was already shipped: " + orderTrackingRecord?.ShipDate + ", no action taken.");
                  //  if (airtableOrderRecord.Channel.ToLower() == "etsy")
                    {
                        throw new Exception("Order shipped more than once: " + airtableOrderRecord?.OrderID);
                    }
                }
                {
                    Log.Add("Record Exists");
                    if (!string.IsNullOrEmpty(orderTrackingRecord.PrintOperator))
                    {
                        {
                            airtableOrderRecord.PrintOperator = orderTrackingRecord.PrintOperator;
                            Log.Add("Setting print operator to " + airtableOrderRecord.PrintOperator.ToString());
                        }

                        airtableOrderRecord.Shipper = orderTrackingRecord.Shipper;
                        Log.Add("Setting shipper to " + airtableOrderRecord.Shipper);

                        airtableOrderRecord.ShipperPay += orderTrackingRecord.ShipperPay;
                        Log.Add("Setting shipper pay to " + airtableOrderRecord.ShipperPay);

                        airtableOrderRecord.ShippingCost = (!string.IsNullOrEmpty(shippingCost))?double.Parse(shippingCost):0;
                        Log.Add("Setting actual shipping cost to " + airtableOrderRecord.ShippingCost.ToString());

                        airtableOrderRecord.ShipDate = DateTime.Now;
                        Log.Add("Setting shipped date to " + airtableOrderRecord.ShipDate.ToString());

                        Log.Add("Order Exists, updating " + orderID.ToString());
                        ATOrdersBase.CreateOrderRecord(airtableOrderRecord, true);
                        ATTrackingBase.CreateOrderRecord(airtableOrderRecord, true);

                        var inventoryBase = new AirtableItemLookup();
                        var inventoryLocationBase = new AirtableInventory();
                        inventoryBase.UpdateCompletedOrderComponentEntries(orderID);

                        if (!dryRun)
                        {
                            foreach(var txnRecordID in orderTrackingRecord.Transactions)
                            {
                                var transactionData = TransactionsBase.GetTransactionByRecordID(txnRecordID);
                                var product = inventoryBase.GetItemRecordByRecordID(transactionData.ItemRecordId);
                                Log.Add($"Ship-time inventory - decremented {product.UniqueName} by {transactionData.Quantity}");
                                inventoryBase.UpdateInventoryCountForTransaction(product, transactionData.Quantity, out var components, orderID);
                                foreach(var component in components)
                                {
                                    Log.Add($"multi location inventory test - decremented {component.Name} at {airtableOrderRecord.Shipper} by {transactionData.Quantity}");
                                    Log.Add($"Component ID: {component.id}");
                                    inventoryLocationBase.IncrementQuantityOfItem(component.id, airtableOrderRecord.Shipper, -transactionData.Quantity);
                                }
                            }
                            //currentTask.Delete();
                        }
                        if (orderTrackingRecord != null)
                        {
                            orderTrackingRecord.ShipDate = airtableOrderRecord.ShipDate;
                        }
                    }
                    else
                    {
                        Log.Add("No owner assigned for " + orderID.ToString());
                    }
                }
            }
            else
            {
                Log.Add("Order was not found in airtable, no action taken");
                return true;
            }
            return true;
        }
        /*
        public void CompleteInventoryRequest(string TaskName)
        {
            var matches = Regex.Match(TaskName, @"\((\d*)\/(\d*)\)\s*(.*)").Groups;
            var componentName = matches[3].Value;
            var component = ATItemLookupBase.GetComponentByName(componentName, false);

            if (component == null)
            {
                throw new Exception("Item was not found in the components database:" + componentName);
            }

            CompleteInventoryRequestCore(component, int.Parse(matches[1].Value), int.Parse(matches[2].Value));
        }
        */
        public void CompleteInventoryRequest(string componentID, int quantityProduced, int quantityRequested, string locationName= "")
        {
            var component = ATItemLookupBase.GetComponentByID(componentID);
  
            Log.Add("Inventory order completed, updating counts for:" + component.Name);
            Log.Add("Update count of " + component.Name + " by +" + quantityProduced.ToString());
            Log.Add("Original request was for " + quantityRequested.ToString());

            ATItemLookupBase.UpdateComponentQuantity(component, quantityProduced, quantityRequested);
            if (!string.IsNullOrEmpty(locationName))
            {
                Log.Add("Updating location-specific count at location:" + locationName);
                ATInventory.IncrementQuantityOfItemByName(component.Name, locationName, quantityProduced);
            }
        }

        public void UpdateCompletedInventoryRequestOrder(string orderID, string componentID, string printOperator, int quantityProduced)
        {
            Log.Add("Checking Order for Order ID: " + orderID);
            var order = ATOrdersBase.GetRecordByOrderID(orderID, out _);
            // we don't create an entry in the pay table until the request is completed in order tracking, so this should always be true
            if (order == null)
            {
                order = ATOrdersBase.newOrderData(orderID);
                Log.Add("getting component by ID: " + componentID);
                var component = ATItemLookupBase.GetComponentByID(componentID);
                Log.Add("found component: " + component.Name);
                order.TotalPrice = component.InternalPrice * quantityProduced;

                order.Channel = "Internal";
                order.Description = $"[INV - {quantityProduced}x] {component.Name}"; ;

                ATOrdersBase.CreateOrderRecord(order);
            }

            order.PrintOperator = printOperator;
            order.Shipper = printOperator;
            Log.Add("Set operator and shipper to " + printOperator);
            if (!(order.ShipDate.Year > 2000))
            {
                Log.Add("Set ship date to now");
                order.ShipDate = DateTime.Now;
            }
            else
            {
                Log.Add("Ship Date already set to " + order.ShipDate.ToString());
            }
            if (quantityProduced == 0)
            {
                Log.Add("no units were produced, not generating order record in pay base");
            }
            else
            {
                ATOrdersBase.CreateOrderRecord(order, true);
            }
            ATTrackingBase.CreateOrderRecord(order, true);
            Log.Add("Updated Order ID " + order.OrderID);
            
        }

        public void ProcessExpense(List<string> log, NameValueCollection fields)
        {
            var ATbase = new AirtableExpenses();

            if (fields["Type"] == "amazon")
            {
                var amazonExpenseEntry = new AmazonExpenseEntry(fields["Body"]);
                {
                    foreach (var expenseEntry in amazonExpenseEntry.expenseEntries)
                    {
                        var airtableExpensesEntry = new ExpensesData(expenseEntry.Name);
                        airtableExpensesEntry.Date = DateTime.Now;
                        airtableExpensesEntry.DeliveredTo = amazonExpenseEntry.ReceiverName;
                        airtableExpensesEntry.Value = expenseEntry.CostForAllItems;
                        airtableExpensesEntry.Quantity = expenseEntry.Quantity;
                        airtableExpensesEntry.OrderId = expenseEntry.orderID;

                        ATbase.CreateExpensesRecord(airtableExpensesEntry);
                    }
                }
                if (amazonExpenseEntry.getOveragesPaid() > .2)
                {
                    var msg = "Amazon expense total does not match sum of entries. creating overage expense";
                    log.Add(msg);
                    var airtableExpensesEntryOverage = new ExpensesData("Unaccounted Amazon Overage");
                    airtableExpensesEntryOverage.Date = DateTime.Now;
                    airtableExpensesEntryOverage.Value = amazonExpenseEntry.getOveragesPaid();
                    ATbase.CreateExpensesRecord(airtableExpensesEntryOverage);


                }
                else if (amazonExpenseEntry.getOveragesPaid() < -.2)
                {
                    var msg = "Amazon expense total is less than sum of entries";
                    log.Add(msg);
                    throw new Exception(msg);
                }

            }
            else //wells transactions are parsed in emailParser for now
            {
                var airtableExpensesEntry = new ExpensesData(fields["Name"]);
                airtableExpensesEntry.Date = DateTime.Now;
                airtableExpensesEntry.DeliveredTo = fields["Delivered To"];
                airtableExpensesEntry.Value = Double.Parse(fields["Order Total"]);

                ATbase.CreateExpensesRecord(airtableExpensesEntry);
            }
        }
        public void ProcessRefund(List<string> log, string orderID, double amountRefunded, string reason)
        {
            var order = ATOrdersBase.GetRecordByOrderID(orderID, out _);       
            if (order != null)
            {
                order.AmountRefunded = amountRefunded;
                ATOrdersBase.CreateOrderRecord(order, true);
                // if the refund is due to a cancellation, mark the order tracking entry card
                // hack because the cancellation emails have a txn ID, not order ID
                {
                    var orderTrackingEntryToRefund = ATTrackingBase.GetRecordByOrderID(orderID, out _);
                    if (orderTrackingEntryToRefund != null)
                    {
                        orderTrackingEntryToRefund.Cancelled = true;
                        ATTrackingBase.UpdateOrderRecord(orderTrackingEntryToRefund);
                    }
                }
            }
            else
            {
                throw new Exception("order to refund not found");
            }
        }

        public void ProcessReturn(List<string> log, string orderID, double labelCost)
        {

            var ATbase = new AirtableExpenses();

            var airtableExpensesEntry = new ExpensesData("Return Label for Etsy Order " + orderID?.ToString());
            airtableExpensesEntry.Date = DateTime.Now;
            airtableExpensesEntry.Value = labelCost;
            airtableExpensesEntry.Quantity = 1;
            airtableExpensesEntry.OrderId = orderID;

            ATbase.CreateExpensesRecord(airtableExpensesEntry);

            var orderTrackingEntryToRefund = ATTrackingBase.GetRecordByOrderID(orderID, out _);
            if (orderTrackingEntryToRefund != null)
            {
                orderTrackingEntryToRefund.Returned = true;
                ATTrackingBase.UpdateOrderRecord(orderTrackingEntryToRefund);
            }
            else
            {
                throw new Exception("order to return not found");
            }
        }

        public void GenerateInventoryRequestByLocation(InventoryComponent component, int quantity, string location)
        {
            var pendingBeforeStart = component.Pending;
            var inventoryLocationEntry = ATInventory.FindRecordByName(component.Name, location);
            string cardName;
            string notes = "";
            var orderType = OrderTrackingData.OrderTypes.InventoryRequest;
            List<string> printerNames = new List<string>();
            var inventoryOrder = ATOrdersBase.newOrderData("I_" + Guid.NewGuid().ToString());
         
            cardName = $"[INV] {component.Name}";
            var preferredPrinter = ATItemLookupBase.GetPreferredPrinter(component);
            inventoryOrder.TotalPrice = component.InternalPrice * quantity;           

            DateTime dueDate = DateTime.Today.AddDays(10);

            inventoryOrder.Channel = "Internal";
            inventoryOrder.Description = cardName;
            inventoryOrder.DueDate = dueDate;
            inventoryOrder.Notes = notes;

            inventoryOrder.Notes += "\n" + component.Details;


            inventoryOrder.PrintOperator = preferredPrinter;

            var inventoryOrderTrackingData = ATTrackingBase.OrderDataToOrderTrackingData(inventoryOrder);
            inventoryOrderTrackingData.IsInventoryRequest = true;
            inventoryOrderTrackingData.SetOrderType(orderType);
            inventoryOrderTrackingData.IncludedComponentId = component.id;
            inventoryOrderTrackingData.RequestedQuantity = quantity;
            inventoryOrderTrackingData.DestinationLocation = location;
            if (!string.IsNullOrEmpty(inventoryOrder.PrintOperator))
            {
                inventoryOrderTrackingData.Stage = "Assigned";
            }
        //    ATOrdersBase.CreateOrderRecord(inventoryOrder);
            ATTrackingBase.CreateOrderRecord(inventoryOrderTrackingData, out _);
            Log.Add("added task to order tracking" + cardName);
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
                var pendingBeforeStart = component.Pending;
                string cardName;
                string notes = "";
                var orderType = OrderTrackingData.OrderTypes.InventoryRequest;
                string inventoryOrderOwner = "";
                string inventoryOrderPrintersString = "";
                List<string> printerNames = new List<string>();
                string preferredInvPrinter = "";
                var inventoryOrder = ATOrdersBase.newOrderData("I_" + Guid.NewGuid().ToString());

                /* Deprecating warehousing
                if (component.WarehouseQuantity >= quantityPerBatch)
                {
                    if (component.WarehouseQuantity < quantityPerBatch * 2)
                    {
                        quantityPerBatch = component.WarehouseQuantity;
                    }
                    cardName = $"[TRANSFER] {component.Name}";
                    orderType = OrderTrackingData.OrderTypes.TransferRequest;
                    component.WarehouseQuantity -= quantityPerBatch;
                    notes = "Transfer Request - do not print";
                    inventoryOrderOwner = warehouseManager;
                }
                */
                // else //not enough in warehouse, do inventory request
                {
                    cardName = $"[INV] {component.Name}";
                    ATItemLookupBase.GetPotentialPrintersList(component, out printerNames, out inventoryOrderOwner);
                    inventoryOrderPrintersString = GetPrinterDetailsString(ref inventoryOrderOwner, printerNames.ToArray(), preferredInvPrinter);
                    inventoryOrder.TotalPrice = component.InternalPrice * quantityPerBatch;
                }

                ATItemLookupBase.LogInventoryRequestCreation(component, quantityPerBatch);

                DateTime dueDate = DateTime.Today.AddDays(10);

                inventoryOrder.Channel = "Internal";
                inventoryOrder.Description = cardName;
                inventoryOrder.DueDate = dueDate;
                inventoryOrder.Notes = notes;


                inventoryOrder.Notes +=
                    "\n" + component.Details +
                    "\n" + inventoryOrderPrintersString +
                    "\n\n" + noteAddendum;


                inventoryOrder.PrintOperator = inventoryOrderOwner;

                var inventoryOrderTrackingData = ATTrackingBase.OrderDataToOrderTrackingData(inventoryOrder);
                inventoryOrderTrackingData.IsInventoryRequest = true;
                inventoryOrderTrackingData.SetOrderType(orderType);
                inventoryOrderTrackingData.IncludedComponentId = component.id;
                inventoryOrderTrackingData.RequestedQuantity = quantityPerBatch;
                if (!string.IsNullOrEmpty(inventoryOrder.PrintOperator))
                {
                    inventoryOrderTrackingData.Stage = "Assigned";
                }
                //ATOrdersBase.CreateOrderRecord(inventoryOrder);
                ATTrackingBase.CreateOrderRecord(inventoryOrderTrackingData, out _);
                Log.Add("added task to order tracking" + cardName);
            }
        }

        private void GenerateOutOfStockNotification(InventoryComponent component)
        {
            var OutOfStockNotification = ATTrackingBase.NewOrderTrackingData("");
            OutOfStockNotification.Description = "Out of stock notification: " + component.Name;
            ATTrackingBase.CreateOrderRecord(OutOfStockNotification, out _);
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
                    return PrintersString;
                }
            }
            //only show preferred printer if there are multiple options
            if (!string.IsNullOrEmpty(preferredPrinter))
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
    class TransactionRecordPair
    {


        public TransactionTypes TransactionType { set; get; }
        public Transaction Transaction { set; get; }
        public InventoryProduct Product { set; get; }


        public string GetDescription()
        {
            return Transaction.GetDescription();
        }
    }
}


