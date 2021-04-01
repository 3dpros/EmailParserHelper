using AirtableClientWrapper;
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

            try
            {
                var result = automation.ProcessOrder(fields["Body"], fields["BodyHTML"], fields["Channel"], out var OrderData, out var orderTracking);
                var operatorString = string.IsNullOrEmpty(orderTracking?.PrintOperator) ? "":"(" + orderTracking.PrintOperator.Split(' ')[0] + ")";
                fields.Add("NotificationEmailSubject", (dryRun ? "(TEST)" : "") + "[" + (OrderData.OrderTotal - OrderData.ShippingCharge).ToString("C") + "]"+ operatorString + " " + OrderData.OneLineDescription);
                log = automation.Log;
                return result;
            }
            catch (Exception e)
            {
                log = automation.Log;
                log.Add(e.Message);
                log.Add(e.StackTrace);
            }
            return false;

        }

        public static bool AddOrderFields(NameValueCollection fields, ref List<string> log, bool dryRun = false)
        {
            fields.Add("NotificationEmailAddresses", dryRun ? "altbillington@gmail.com" : "altbillington@gmail.com, daniel.c.english@gmail.com");
            var automation = new Automation(dryRun);

            try
            {
                var result = automation.ProcessOrderFromFields(fields, fields["Channel"], out var OrderData, out var orderTracking);
                var operatorString = string.IsNullOrEmpty(orderTracking?.PrintOperator) ? "" : "(" + orderTracking.PrintOperator.Split(' ')[0] + ")";
                fields.Add("NotificationEmailSubject", (dryRun ? "(TEST)" : "") + "[" + (OrderData.OrderTotal - OrderData.ShippingCharge).ToString("C") + "]" + operatorString + " " + OrderData.OneLineDescription);
                log = automation.Log;
                return result;
            }
            catch (Exception e)
            {
                log = automation.Log;
                log.Add(e.Message);
                log.Add(e.StackTrace);
            }
            return false;

        }

        public static bool ProcessShippedProductOrder(ref List<string> log, NameValueCollection fields, bool dryRun = false)
       {         
            var automation = new Automation(dryRun);
            try
            {
                log.Add("starting complete order");
                var result = automation.CompleteOrder(fields["Order ID"], fields["Shipping Cost"]);
                log = automation.Log;

                return result;
            }
            catch (Exception e)
            {
                log = automation.Log;
                log.Add("exception occurred");
                log.Add(e.Message);
                log.Add(e.Source);
                log.Add(e.InnerException?.Message);
                log.Add(e.InnerException?.StackTrace);
            }
            return false;
        }

        public static bool CompleteInventoryRequestOrder(NameValueCollection fields, ref List<string> log, bool dryRun)
        {
            var automation = new Automation(dryRun);
            try
            {
                log.Add("starting complete inventory request order: " + fields["Task Name"]);
                int.TryParse(fields["Requested Quantity"], out int requestedQuantity);
                int.TryParse(fields["Produced Quantity"], out int producedQuantity);

                automation.CompleteInventoryRequest(fields["Component ID"], producedQuantity, requestedQuantity, fields["Location"]);
                log.Add("updated inventory quantities.");

                if (!string.IsNullOrEmpty(fields["Order ID"]))
                {
                    automation.UpdateCompletedInventoryRequestOrder(fields["Order ID"], fields["Component ID"], fields["Owner"], producedQuantity);
                }
                else
                {
                    log.Add("no identifier found in fields, cannot update order record");
                }
                log = automation.Log;

                return true;
            }
            catch (Exception e)
            {
                log = automation.Log;
                log.Add(e.StackTrace);
                log.Add(e.Message);

            }

            return false;
        }

        public static bool CreateManualInventoryOrder(NameValueCollection fields)
        {
            var inventoryBase = new AirtableItemLookup();
            var auto = new Automation();
            var component = inventoryBase.GetComponentByName(fields["Item Name"], false);
            if (component != null)
            {
                auto.GenerateInventoryRequest(component, int.Parse(fields["Quantity"]));
                return true;
            }
            return false;
        }

        public static bool CreateAutomaticInventoryOrder(string componentID)
        {
            var inventoryBase = new AirtableItemLookup();
            var auto = new Automation();
            var component = inventoryBase.GetComponentByID(componentID);
            if (component != null)
            {
                auto.GenerateInventoryRequest(component);
                return true;
            }
            return false;
        }

        public static bool CreateAutomaticInventoryOrderByLocation(NameValueCollection fields)
        {
            var inventoryBase = new AirtableItemLookup();
            var auto = new Automation();

            var componentID = fields["Component ID"];
            var location = fields["Location"];
            int.TryParse(fields["Quantity"], out int requestedQuantity);

            var component = inventoryBase.GetComponentByID(componentID);
            if (component != null)
            {
                auto.GenerateInventoryRequestByLocation(component, requestedQuantity, location);
                return true;
            }
            return false;
        }

        public static bool ProcessExpense(ref List<string> log, NameValueCollection fields)
        {
            var auto = new Automation();
            auto.ProcessExpense(log, fields);
            return true;
        }

        public static bool ProcessRefund(ref List<string> log, NameValueCollection fields)
        {
            var auto = new Automation();
            double amount;
            double.TryParse(fields["Refund Amount"], out amount);
            var orderID = fields["Order ID"];
            var reason = fields["Reason"];
            auto.ProcessRefund(log, orderID, amount, reason);
            log.Add($"refunded order {orderID} for {amount}");
            return true;
        }

    }

}
