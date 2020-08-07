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

        public string orderID { set; get; }
        public bool isTotalValid()
        {
            double actualTotal = 0;
            foreach(var expense in expenseEntries)
            {
                actualTotal += expense.CostForAllItems;
            }
            return (actualTotal == NominalOrderTotal);
        }
    }

}
