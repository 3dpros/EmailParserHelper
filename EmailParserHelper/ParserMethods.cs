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

            try
            {
                Order OrderData;
                var result = automation.ProcessOrder(fields["body"], fields["Channel"], out OrderData);
                fields.Add("NotificationEmailSubject", (dryRun ? "(TEST)" : "") + "[" + (OrderData.OrderTotal - OrderData.ShippingCharge).ToString("C") + "]" + OrderData.OneLineDescription);
                log = automation.Log;
                return result;
            }
            catch (Exception e)
            {
                log = automation.Log;
                log.Add(e.Message);
                log.Add(e.StackTrace);
                log.Add(e.ToString());
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
                log.Add(e.Message);
            }

            return false;
        }

        public static bool CompleteInventoryRequestOrder(NameValueCollection fields, ref List<string> log, bool dryRun)
        {
            var automation = new Automation(dryRun);
            try
            {
                automation.CompleteInventoryRequest(fields["Task Name"]);
                if (!string.IsNullOrEmpty(fields["Task ID"]))
                {
                    automation.UpdateCompletedInventoryRequestOrderAsana(fields["Task ID"], fields["Owner"]);
                }
                else if (!string.IsNullOrEmpty(fields["Order ID"]))
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

}
