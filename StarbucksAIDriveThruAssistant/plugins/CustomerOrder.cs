using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plugins
{
    public  class CustomerOrder
    {
        private string size;
        private string beverage;
        private string milktype;
        private List<CustomerOrder> orders; // List to store orders
       /* public CustomerOrder(string size, string beverage, string milktype) {
        
            this.size = size;
            this.beverage = beverage;
            this.milktype = milktype;
            
        }*/

        public  void setSize(string size)
        {
            this.size = size;
        }

        public void setBeverage(string beverage)
        {
            this.beverage = beverage;
        }

        public void setMilk(string milktype)
        {
            this.milktype= milktype;
        }



        //Getters
        public string getSize()
        {
            return size;
        }
        public string getBeverage()
        {
            return beverage;
        }
        public string getMilktype()
        {
            return milktype;
        }

    }
}
