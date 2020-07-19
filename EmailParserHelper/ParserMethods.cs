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

        public static bool ProcessShippedProductOrder(ref List<string> log, NameValueCollection fields, bool dryRun = false)
        {
            var automation = new Automation(dryRun);
            try
            {
                var result = automation.CompleteOrder(fields["Order ID"], fields["Shipping Cost"]);
                log = automation.Log;

                return result;
            }
            catch (Exception e)
            {
                log = automation.Log;
                log.Add(e.InnerException.Message);
                log.Add(e.StackTrace);
            }

            return false;
        }

        public static bool CompleteInventoryRequestOrder(NameValueCollection fields, ref List<string> log, bool dryRun)
        {
            var automation = new Automation(dryRun);
            try
            {
                automation.CompleteInventoryRequest(fields["Task Name"]);
                if (!string.IsNullOrEmpty(fields["Order ID"]))
                {
                    automation.UpdateCompletedInventoryRequestOrderAirtable(fields["Order ID"], fields["Owner"]);
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
                log.Add(e.Message);
            }

            return false;
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

    }

}
