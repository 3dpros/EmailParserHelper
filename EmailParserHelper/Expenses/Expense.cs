using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailParserHelper
{
    public class ExpenseEntry
    {
        public string Name { set; get; }
        public double CostPerItem { set; get; }
        public int Quantity { get; set; } = 1;
        public double CostForAllItems
        {
            get
            {
                return CostPerItem * Quantity;
            }
        }
        public string orderID { set; get; }

    }

    public class ExpenseOrder
    {
        public ExpenseOrder(string emailBody)
        {
            EmailBody = emailBody;
        }
        protected string EmailBody;
        public List<ExpenseEntry> expenseEntries { get; set; } = new List<ExpenseEntry>();
        public string ReceiverName { set; get; }
        public double NominalOrderTotal { set; get; }

        public double getOveragesPaid()
        {
            double calculatedTotal = 0;
            foreach(var expense in expenseEntries)
            {
                calculatedTotal += expense.CostForAllItems;
            }
            return NominalOrderTotal - calculatedTotal;
        }
    }

}
