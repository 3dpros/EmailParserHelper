using AirtableClientWrapper;
using AsanaAPI;
using AsanaNet;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailParserHelper
{
    public class ParserMethods
    {
        public static bool AddOrder(NameValueCollection fields, ref List<string> log, bool dryRun = false)
        {
            fields.Add("NotificationEmailAddresses", dryRun ? "altbillington@gmail.com" : "altbillington@gmail.com, daniel.c.english@gmail.com");

            var automation = new Automation(dryRun);
            Order OrderData;
            var result = automation.ProcessOrder(fields["body"], fields["Channel"], out OrderData);
            fields.Add("NotificationEmailSubject", (dryRun ? "(TEST)" : "") + "[" + (OrderData.OrderTotal - OrderData.ShippingCharge).ToString("C") + "]" + OrderData.ShortDescription);

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

    public static bool ProcessShippedProductOrder(ref List<string> log, NameValueCollection fields)
    {
        // try{
        log.Add("Starting Airtable Entry");
        var ATbase = new AirtableOrders();
        var Asana = new AsanaWrapper("0/800035e25a4534df363da6babe19cd1b", "3D Pros", "Etsy Orders");

        string orderID = fields["Order ID"];

        log.Add("Checking Order " + orderID);

        var ATid = "";
        var airtableOrderRecord = ATbase.GetRecordByOrderID(orderID, out ATid);
        log.Add("Got Airtable Record");
        if (airtableOrderRecord != null)
        {
            log.Add("Record Exists");

            var asanaID = airtableOrderRecord.AsanaTaskID;
            if(!(airtableOrderRecord.ShipDate == null || airtableOrderRecord.ShipDate.Year < 2010))
        	{
        	    log.Add("Shipper already set to  " + airtableOrderRecord.Shipper + ", no action taken.");
        	    return true;
        	}
            if (asanaID != "")
                log.Add("Checking Asana ID " + asanaID);
            AsanaTask currentTask = Asana.GetTaskByTaskID(Int64.Parse(asanaID));
            log.Add("Found Asana Task For " + asanaID);
            if (currentTask.Assignee != null)
            {

            	if(currentTask.Assignee != null)
            	{
	                airtableOrderRecord.PrintOperator = currentTask.Assignee.Name;
	                log.Add("Setting print operator to " + airtableOrderRecord.PrintOperator.ToString());
                }
                
                airtableOrderRecord.Shipper = GetShipperName(currentTask, currentTask.Assignee.Name);
                log.Add("Setting shipper to " + airtableOrderRecord.Shipper);
                
                airtableOrderRecord.ShippingCost = double.Parse(fields["Shipping Cost"]);
                log.Add("Setting actual shipping cost to " + airtableOrderRecord.ShippingCost.ToString());

                airtableOrderRecord.ShipDate = DateTime.Now;
                log.Add("Setting shipped date to " + airtableOrderRecord.ShipDate.ToString());
                currentTask.Delete();

                log.Add("Order Exists, updating " + orderID.ToString());
                ATbase.CreateOrderRecord(airtableOrderRecord, true);
                var inventoryBase = new AirtableItemLookup();
                inventoryBase.UpdateCompletedOrderComponentEntries(orderID);
            }
            else
            {
                log.Add("No owner assigned for " + orderID.ToString());
            }
        }
        else
        {
            log.Add("Order was not found in airtable, no action taken");
            return true;
        }
        return true;
    }

    private static string GetShipperName(AsanaTask currentTask, string printOperator)
    {
        var shipperNameMap = new Dictionary<string, string>()
        {
            {"Shipper", "Leah"}
        };
		
		var projectNames = new List<string>();
        foreach (var project in currentTask.Projects)
        {
            projectNames.Add(project.Name);
        }

        foreach (var item in shipperNameMap)
        {
            if (projectNames.Contains(item.Key))
            {
                return item.Value;
            }
        }
        return printOperator;
    }

    }

}
